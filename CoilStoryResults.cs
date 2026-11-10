using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

public readonly record struct CoilDayStats(
    double MaxAltitude, double FlightTimeMs, double MaxSpeed,
    double MaxDistanceLeft = 0, double MaxDistanceRight = 0, int ObstaclesHit = 0,
    double MoneyMultiplier = 1, double MoneyFlat = 0);

public readonly record struct CoilDayReward(double Cash, double BonusPoints, double BonusEarnings, double Interest);

// Result.as and ResultScreen.as place the result over the still-visible flight.
public partial class CoilStoryResults : Control
{
    private const string Art = "res://Assets/Results/";
    private const int FramesPerSecond = 65; // Global.defaultFramerate, also the SWF header.
    private const int ShowDelay = 75;
    private const int ShowDuration = 80;
    private const int RowSpacing = 55;
    private const int DimEndLabel = 70;
    private const int DimLastFrame = 73;

    private readonly record struct RewardRow(string Label, int Cash, double Progress, float Hue, Color Color);

    private sealed class AnimatedRow(RewardRow data, int delay, Label cash, AtlasTexture progress, TextureRect best, AtlasTexture bestAtlas)
    {
        private int _frame;
        private int _duration = 70;
        private int _delay = delay;
        private int _bestFrame;

        public void Advance()
        {
            if (_bestFrame > 0)
            {
                if (_bestFrame < 60) SetFrame(bestAtlas, ++_bestFrame, 5, 805, 73);
                return;
            }
            if (_frame >= _delay + _duration) return;
            double t = Math.Clamp(++_frame - _delay, 0, _duration) / (double)_duration;
            double progressEase = t < .5 ? 8 * Math.Pow(t, 4) : 1 - 8 * Math.Pow(1 - t, 4);
            SetFrame(progress, (int)Math.Floor(1 + 399 * data.Progress * progressEase), 7, 508, 74);
            int displayedCash = t == 1 ? data.Cash : (int)Math.Floor(data.Cash * (1 - Math.Pow(1 - t, 4)));
            cash.Text = Money(displayedCash);
            if (t == 1 && data.Progress >= 1)
            {
                _bestFrame = 1;
                best.Visible = true;
            }
        }

        // Node.as::finishProgressBarNow changes the duration, then advances one frame.
        public void FinishNow()
        {
            if (_frame >= _duration) return;
            _delay = 0;
            _duration /= 2;
            _frame = Math.Min(_duration - 1, _frame);
            Advance();
        }
    }

    private int _day;
    private RewardRow[] _rewards = [];
    private CoilDayReward _reward;
    private Func<CoilDayReward, bool> _commit = null!;
    private Action _returnToShop = null!;
    private bool _leaving;
    private bool _animationCompleted;
    private double _frameRemainder;
    private int _frame;
    private int _dimPage = -1;
    private int _dimFrame;
    private bool _dimSkipped;
    private Control _panel = null!;
    private AtlasTexture _dimAtlas = null!;
    private FontFile _font = null!;
    private readonly List<AnimatedRow> _rows = new();

    public static void Show(Node flight, CoilDayStats stats, Func<CoilDayReward, bool> commit, Action returnToShop)
    {
        PortGameSlot slot = PortProfile.CurrentSlot;
        (RewardRow[] rows, CoilDayReward reward) = ComputeRewards(slot, stats);
        var layer = new CanvasLayer { Layer = 20 };
        var results = new CoilStoryResults
        {
            Name = "CoilStoryResults",
            _day = slot.Day,
            _rewards = rows,
            _reward = reward,
            _commit = commit,
            _returnToShop = returnToShop,
        };
        flight.AddChild(layer);
        layer.AddChild(results);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _font = GD.Load<FontFile>(Art + "154_Damn Noisy Kids.ttf");

        _dimAtlas = new AtlasTexture { FilterClip = true };
        SetDimFrame(1);
        AddArt(this, _dimAtlas, Vector2.Zero);

        _panel = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_panel);
        PositionPanel(760);

        AddArt(_panel, GD.Load<Texture2D>(Art + "top.png"), new(-250.5f, -72));
        AddText(_panel, $"Results for day {_day}", new(-234.05f, -54.45f), 483.4f, 34, Colors.White, 4);

        float height = _rewards.Length * RowSpacing;
        // ResultBg.as masks its panel and all child rows to the 720 px story viewport.
        var body = new Control
        {
            Position = new Vector2(-360, 0), Size = new Vector2(720, height),
            ClipContents = true, MouseFilter = MouseFilterEnum.Ignore,
        };
        _panel.AddChild(body);
        AddArt(body, GD.Load<Texture2D>(Art + "background.png"), new(109.5f, 0));
        Texture2D progress = GD.Load<Texture2D>(Art + "progress.png");
        Texture2D best = GD.Load<Texture2D>(Art + "best.png");
        Shader hueShader = GD.Load<Shader>(Art + "progress-hue.gdshader");
        for (int i = 0; i < _rewards.Length; i++) AddRow(body, _rewards[i], i, progress, best, hueShader);

        AddArt(_panel, GD.Load<Texture2D>(Art + "bottom.png"), new(-250.5f, height - 3));
        AddText(_panel, Money(_reward.Cash), new(-105.9f, height + 7.6f), 343.8f, 40, Colors.White, 4, true);
        OriginalMusic.PlayResults(OriginalMusic.Create(this));
    }

    private void AddRow(Control parent, RewardRow row, int index, Texture2D progress, Texture2D best, Shader shader)
    {
        var root = new Control { Position = new Vector2(360, index * RowSpacing), MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(root);

        var progressAtlas = new AtlasTexture { Atlas = progress, FilterClip = true };
        SetFrame(progressAtlas, 1, 7, 508, 74);
        var progressArt = AddArt(root, progressAtlas, new(-251.125f, -11.25f));
        var hue = new ShaderMaterial { Shader = shader };
        hue.SetShaderParameter("hue", Mathf.DegToRad(row.Hue));
        progressArt.Material = hue;

        var bestAtlas = new AtlasTexture { Atlas = best, FilterClip = true };
        SetFrame(bestAtlas, 1, 5, 805, 73);
        var bestArt = AddArt(root, bestAtlas, new(-566, -13.15f));
        bestArt.Visible = false;

        AddText(root, row.Label, new(-231.05f, 8.25f), 338.05f, 30, row.Color, 2);
        var cash = AddText(root, "", new(-25, 8.25f), 263.45f, 30, row.Color, 2, true);
        _rows.Add(new AnimatedRow(row, 80 + index * 20, cash, progressAtlas, bestArt, bestAtlas));
    }

    private static TextureRect AddArt(Control parent, Texture2D texture, Vector2 position)
    {
        var art = new TextureRect
        {
            Position = position, Size = texture.GetSize(), Texture = texture,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        parent.AddChild(art);
        return art;
    }

    private Label AddText(Control parent, string text, Vector2 position, float width, int size, Color color, int outline, bool right = false)
    {
        var label = new Label
        {
            Text = text, Position = position, Size = new Vector2(width, size + 12),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", outline);
        parent.AddChild(label);
        return label;
    }

    public override void _Process(double delta)
    {
        if (_leaving) return;
        _frameRemainder += delta * FramesPerSecond;
        int frames = (int)_frameRemainder;
        _frameRemainder -= frames;
        for (int i = 0; i < frames; i++)
        {
            _frame++;
            foreach (var row in _rows) row.Advance();
        }
        if (!_dimSkipped && _dimFrame < DimLastFrame) SetDimFrame(Math.Min(DimLastFrame, _frame + 1));
        if (_animationCompleted || _frame <= ShowDelay) return;
        PositionPanel(ElasticY(Math.Min(ShowDuration - 1, _frame - ShowDelay - 1)));
        _animationCompleted = _frame >= ShowDelay + ShowDuration;
    }

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false }
            && input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
        GetViewport().SetInputAsHandled();
        if (_leaving) return;
        if (!_animationCompleted)
        {
            foreach (var row in _rows) row.FinishNow();
            _dimSkipped = true;
            SetDimFrame(DimEndLabel);
            PositionPanel(ElasticY(ShowDuration));
            _animationCompleted = true;
            return;
        }
        if (_commit(_reward))
        {
            _leaving = true;
            _returnToShop();
        }
    }

    private void SetDimFrame(int frame)
    {
        if (frame == _dimFrame) return;
        _dimFrame = frame;
        int index = frame - 1;
        int page = index / 8;
        if (page != _dimPage)
        {
            _dimAtlas.Atlas = GD.Load<Texture2D>($"{Art}dim-{page}.png");
            _dimPage = page;
        }
        SetFrame(_dimAtlas, index % 8 + 1, 4, 960, 540);
    }

    private static void SetFrame(AtlasTexture atlas, int frame, int columns, int width, int height)
    {
        int index = frame - 1;
        atlas.Region = new Rect2(index % columns * width, index / columns * height, width, height);
    }

    private void PositionPanel(float y) => _panel.Position = new Vector2(GetViewportRect().Size.X / 2, y);

    private float ElasticY(int frame)
    {
        const double start = 760;
        const double period = 100;
        double end = 270 - _rewards.Length * RowSpacing * .5;
        if (frame == 0) return (float)start;
        if (frame == ShowDuration) return (float)end;
        double change = end - start;
        return (float)(change * Math.Pow(2, -10 * frame / (double)ShowDuration)
            * Math.Sin((frame - period / 4) * Math.Tau / period) + end);
    }

    private static string Money(double amount) => "$ " + amount.ToString("N0", CultureInfo.InvariantCulture);

    private static (RewardRow[] Rows, CoilDayReward Reward) ComputeRewards(PortGameSlot slot, CoilDayStats stats)
    {
        // Global.as:206-264. Keep the pre-rounding values for dependent rewards.
        double altitude = (stats.MaxAltitude + 60) * .009 + Math.Pow((stats.MaxAltitude + 60) * .001, 1.6);
        double time = Math.Max(stats.FlightTimeMs - 4250, 0);
        double duration = time <= 40000 ? Lerp(0, 500, time / 40000, 1.3) : 500 + (time - 40000) / 30;
        double durationBlend = Lerp(0, .35, time / 240000, .6);
        duration = CapDuration(duration * (1 - durationBlend) + altitude * durationBlend);
        double speed = Math.Max(stats.MaxSpeed - 4.5, 0) * .7;
        double speedReward = CapSpeed((speed + Math.Pow(speed * .17, 2.7)) * .7
            + altitude * Lerp(0, .2, speed / 50, .6) + duration * Lerp(0, .1, speed / 50, .6));

        var main = new Color("333333");
        var rows = new List<RewardRow>
        {
            new("Altitude reached", (int)altitude, Ratio(stats.MaxAltitude, slot.BestAltitude), -12, main),
            new("Air time", (int)duration, Ratio(stats.FlightTimeMs, slot.BestDuration), 35, main),
            new("Max speed", (int)speedReward, Ratio(stats.MaxSpeed, slot.BestSpeed), 100, main),
        };
        double subtotal = altitude + duration + speedReward;
        double bonus = stats.MoneyFlat + Math.Floor(subtotal * stats.MoneyMultiplier - subtotal + .5);
        if (bonus != 0) rows.Add(new("Bonus earnings", (int)bonus, Ratio(bonus, slot.BestBonusMoney), -70, new Color("444444")));

        var entries = OriginalCatalog.Load();
        double interest = 0;
        int interestLevel = PortProfile.State.BonusLevels.GetValueOrDefault("bonusInterest");
        if (interestLevel >= 1)
        {
            var item = entries.First(e => e.id == "bonusInterest");
            interest = Math.Floor(Math.Clamp(slot.Cash * item.Value("statPower", interestLevel), 0, item.Value("statFuel", interestLevel)) + .5);
            if (interest != 0) rows.Add(new("Daily interest", (int)interest, Ratio(interest, slot.BestInterestEarnings), -105, new Color("555555")));
        }
        int bpLevel = PortProfile.State.BonusLevels.GetValueOrDefault("bonusBpPerDay");
        double bp = entries.First(e => e.id == "bonusBpPerDay").Value("statPower", bpLevel);
        double cash = rows.Sum(r => (double)r.Cash);
        return (rows.ToArray(), new CoilDayReward(cash, bp, bonus, interest));
    }

    private static double Ratio(double value, double best) => value <= 0 ? 0 : value / Math.Max(value, best);

    private static double Lerp(double a, double b, double t, double exponent) => a + (b - a) * Math.Pow(Math.Clamp(t, 0, 1), exponent);

    private static double CapDuration(double value)
    {
        if (value <= 3500) return value;
        double remainder = value - 3500;
        double total = 3500;
        for (double scale = .99; scale > .05; scale -= .01)
        {
            if (remainder <= 100) return total + remainder * scale;
            total += 100 * scale;
            remainder -= 100;
        }
        return total + remainder * .05;
    }

    private static double CapSpeed(double value)
    {
        if (value <= 1000) return value;
        double remainder = value - 1000;
        double total = 1000;
        for (int step = 0; step <= 100; step++)
        {
            double scale = Lerp(1, .01, step / 100d, .05);
            if (remainder <= 490) return total + remainder * scale;
            total += 490 * scale;
            remainder -= 490;
        }
        return total + remainder * .01;
    }
}
