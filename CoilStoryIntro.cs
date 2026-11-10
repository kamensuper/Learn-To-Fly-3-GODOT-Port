using Godot;
using System;

// Cutscene.playNewGameIntro uses cutscene.NewGame (SWF symbol 6147) at 24 fps.
// The lossless frame archive is rendered from that symbol; CutsceneEvents uses its clock.
public partial class CoilStoryIntro : Control
{
    public static bool IsActive { get; private set; }

    private const string Art = "res://Assets/NewGame/StoryIntro/";
    private const int FramesPerSecond = 24;
    private const int LastFrame = 1721;
    private static readonly int[] CueFrames =
    {
        1, 110, 403, 755, 783, 951, 1052, 1075, 1094, 1123,
        1247, 1360, 1470, 1626, 1637, 1709
    };

    private Action? _finished;
    private ZipReader? _frames;
    private TextureRect _art = null!;
    private ImageTexture? _frameTexture;
    private TextureButton _skip = null!;
    private TextureRect _speaker = null!;
    private Label _speakerLabel = null!;
    private MouseBounce2? _speakerBounce;
    private AudioStreamPlayer _music = null!;
    private AudioStreamPlayer? _boing;
    private AudioStreamPlayer? _steps;
    private int _nextCue;
    private int _shownFrame;
    private double _elapsed;
    private bool _ending;

    public static void Play(Node owner, Action onFinished)
    {
        var intro = new CoilStoryIntro { _finished = onFinished };
        owner.AddChild(intro);
    }

    public override void _Ready()
    {
        IsActive = true;
        OriginalAudio.StopAll();
        ZIndex = 100;
        Size = new Vector2(960, 540);
        MouseFilter = MouseFilterEnum.Stop;

        _art = new TextureRect
        {
            Size = Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_art);
        _music = OriginalMusic.Create(this);

        // SkipButton symbol 3614, positioned by UiScaler.addAbsolute.
        // The exported frames include the renderer's 5 px border.
        var skipMask = new Bitmap();
        skipMask.CreateFromImageAlpha(GD.Load<Texture2D>(Art + "skip-hit.png").GetImage());
        _skip = new TextureButton
        {
            Position = new Vector2(864.22f, 489.4f),
            TextureNormal = GD.Load<Texture2D>(Art + "skip-up.png"),
            TextureHover = GD.Load<Texture2D>(Art + "skip-over.png"),
            TexturePressed = GD.Load<Texture2D>(Art + "skip-down.png"),
            TextureClickMask = skipMask,
            FocusMode = FocusModeEnum.None
        };
        AddChild(_skip);
        _skip.Pressed += () => End(true);
        AddSystemControls();

        _frames = new ZipReader();
        if (_frames.Open(Art + "frames.zip") != Error.Ok || !ShowFrame(1))
        {
            GD.PushError("Missing original LTF3 Story intro frames");
            Callable.From(() => End(true)).CallDeferred();
            return;
        }
        PlayCuesThrough(1);
    }

    public override void _Process(double delta)
    {
        if (_ending) return;
        _elapsed += delta;
        int frame = Math.Min(LastFrame, 1 + (int)Math.Floor(_elapsed * FramesPerSecond));
        if (frame == _shownFrame) return;
        if (!ShowFrame(frame))
        {
            End(true);
            return;
        }
        PlayCuesThrough(frame);
        if (frame >= LastFrame - 1) End();
    }

    private bool ShowFrame(int frame)
    {
        var buffer = _frames!.ReadFile($"{frame}.webp");
        using var image = new Image();
        if (buffer.Length == 0 || image.LoadWebpFromBuffer(buffer) != Error.Ok)
        {
            GD.PushError($"Could not read original LTF3 Story intro frame {frame}");
            return false;
        }
        image.Convert(Image.Format.Rgba8);
        if (_frameTexture == null)
        {
            _frameTexture = ImageTexture.CreateFromImage(image);
            _art.Texture = _frameTexture;
        }
        else
        {
            _frameTexture.Update(image);
        }
        _shownFrame = frame;
        return true;
    }

    public override void _Input(InputEvent input)
    {
        if (_ending || input is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape) return;
        GetViewport().SetInputAsHandled();
        End();
    }

    private void AddSystemControls()
    {
        _speaker = new TextureRect
        {
            Texture = OriginalAudio.SpeakerTexture(),
            // Main.initSystem: (54, 21); symbol 3619 starts at (-310, -377) twips.
            Position = new Vector2(38.5f, 2.15f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_speaker);
        _speaker.PivotOffset = new Vector2(15.5f, 18.85f);
        _speakerBounce = new MouseBounce2(_speaker);
        _speakerBounce.SignalClick += _ =>
        {
            OriginalAudio.ToggleMute();
            _speaker.Texture = OriginalAudio.SpeakerTexture();
            ShowSpeakerLabel();
        };
        _speakerBounce.SignalOver += _ => ShowSpeakerLabel();
        _speakerBounce.SignalOut += _ => _speakerLabel.Hide();

        _speakerLabel = new Label
        {
            Position = new Vector2(9, 40),
            Size = new Vector2(90, 23),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
        _speakerLabel.AddThemeFontOverride("font", GD.Load<FontFile>("res://Assets/Start/numbers.ttf"));
        _speakerLabel.AddThemeFontSizeOverride("font_size", 18);
        _speakerLabel.AddThemeConstantOverride("outline_size", 6);
        _speakerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_speakerLabel);

        var fullscreen = new TextureButton
        {
            TextureNormal = GD.Load<Texture2D>("res://Assets/Start/fullscreen.png"),
            Position = new Vector2(89.2f, -3.8f),
            Scale = Vector2.One * .85f,
            FocusMode = FocusModeEnum.None
        };
        AddChild(fullscreen);
        fullscreen.Pressed += () =>
        {
            var window = GetWindow();
            window.Mode = window.Mode == Window.ModeEnum.Fullscreen
                ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
        };
    }

    private void ShowSpeakerLabel()
    {
        _speakerLabel.Text = OriginalAudio.IsMuted ? "Unmute" : "Mute";
        _speakerLabel.Show();
    }

    private void PlayCuesThrough(int frame)
    {
        while (_nextCue < CueFrames.Length && CueFrames[_nextCue] <= frame)
        {
            switch (CueFrames[_nextCue++])
            {
                case 1:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroWind);
                    break;
                case 110:
                    OriginalMusic.Play(_music, OriginalMusic.StoryIntro, gain: .65f, fadeInMs: 600);
                    break;
                case 403:
                    _boing = OriginalAudio.PlaySfx(this, OriginalSound.IntroBoing, loop: true, fadeInMs: 1500);
                    break;
                case 755:
                    OriginalAudio.FadeOut(_boing, 1000);
                    break;
                case 783:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroExplosion);
                    break;
                case 951:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroDummyCrash);
                    break;
                case 1052:
                case 1075:
                case 1094:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroPenguinStep);
                    break;
                case 1123:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroBookBang);
                    break;
                case 1247:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroKnock);
                    break;
                case 1360:
                    OriginalAudio.FadeOut(_music, 3500);
                    break;
                case 1470:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroBuildUpWhoosh);
                    break;
                case 1626:
                    OriginalAudio.PlaySfx(this, OriginalSound.IntroDoorBust);
                    break;
                case 1637:
                    _steps = OriginalAudio.PlaySfx(this, OriginalSound.IntroPenguinStep,
                        loop: true, fadeInMs: 300, gain: .5f);
                    break;
                case 1709:
                    OriginalAudio.FadeOut(_steps);
                    break;
            }
        }
    }

    private void End(bool skip = false)
    {
        if (_ending) return;
        _ending = true;
        if (skip) OriginalAudio.StopAll();
        _skip.Hide();
        _art.Texture = GD.Load<Texture2D>(Art + "final.png");

        var overlay = new TransitionOverlay();
        GetTree().Root.AddChild(overlay);
        overlay.PlayOut(() =>
        {
            Hide();
            QueueFree();
            _finished?.Invoke();
            overlay.PlayIn();
        });
    }

    public override void _ExitTree()
    {
        _speakerBounce?.Destroy();
        _frames?.Close();
        _frames?.Dispose();
        IsActive = false;
    }
}
