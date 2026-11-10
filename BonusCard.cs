using Godot;
using System;

// Stock UpgradeBar display list, with the fields and item preview filled at runtime.
public partial class BonusCard : Control
{
    public event Action<int>? LevelChangeRequested;
    public event Action? Closed;
    private Label _name = null!, _level = null!, _info = null!, _cost = null!;
    private TextureRect _preview = null!;
    private Tween? _motion;
    private Vector2 _rest;
    private bool _closing;
    private BonusItemState _item = null!;

    public override void _Ready()
    {
        _rest = Position;
        Size = new Vector2(301, 540);
        MouseFilter = MouseFilterEnum.Stop;
        Hide();
        var monitor = GD.Load<Texture2D>("res://Assets/Bonus/upgrade-monitor.png");
        Art(new AtlasTexture { Atlas = monitor, Region = new Rect2(666, 405, 302, 535) }, Vector2.Zero, Size);
        _preview = Art(null, new(64, 24), new(215, 145));
        _preview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _name = Field(23, new(48, 174), 245, Colors.White);
        _level = Field(17, new(48, 203), 245, new Color("#a5bdcb"));
        _info = Field(14, new(54, 235), 229, Colors.White);
        _cost = Field(16, new(49, 385), 245, Colors.White);
        Art(GD.Load<Texture2D>("res://Assets/Bonus/upgrade_bar_ui.png"), new(53, 452), new(235, 44));
        Hit(new(53, 458), new(32, 33), () => ChangeLevel(-1));
        Hit(new(249, 452), new(40, 43), () => ChangeLevel(1));
        Hit(new(52, 501), new(90, 33), HideCard);
    }

    private TextureRect Art(Texture2D? texture, Vector2 position, Vector2 size)
    {
        var art = new TextureRect { Position = position,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        art.Texture = texture;
        AddChild(art);
        art.Size = size;
        return art;
    }

    private Label Field(int size, Vector2 position, int width, Color fill)
    {
        var field = new Label { Position = position, Size = new(width, 0), HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        field.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Start/numbers.ttf"));
        field.AddThemeFontSizeOverride("font_size", size);
        field.AddThemeColorOverride("font_color", fill);
        field.AddThemeColorOverride("font_outline_color", Colors.Black);
        field.AddThemeConstantOverride("outline_size", 4);
        AddChild(field);
        return field;
    }

    private void Hit(Vector2 position, Vector2 size, Action action)
    {
        var button = new Button { Flat = true, Position = position, Size = size, FocusMode = FocusModeEnum.None };
        button.Pressed += action;
        AddChild(button);
    }

    private void ChangeLevel(int amount)
    {
        int next = Math.Clamp(_item.Level + amount, 0, _item.UpgradeLimit);
        if (next == _item.Level) return;
        LevelChangeRequested?.Invoke(amount);
        Refresh();
    }

    public void Show(BonusItemState item)
    {
        _item = item;
        Refresh();
        if (Visible && !_closing) return;
        _motion?.Kill();
        if (!Visible) Position = _rest + new Vector2(350, 0);
        _closing = false;
        Show();
        Slide(_rest);
    }

    public void HideCard()
    {
        if (_closing || !Visible) return;
        _closing = true;
        Closed?.Invoke();
        Slide(_rest + new Vector2(350, 0));
        _motion!.Finished += Hide;
    }

    private void Slide(Vector2 destination)
    {
        _motion?.Kill();
        _motion = CreateTween();
        _motion.TweenProperty(this, "position", destination, 40 / 60.0)
            .SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
    }

    private void Refresh()
    {
        BonusData.UpdateDescription(_item);
        _name.Text = _item.Name;
        _level.Text = _item.Level >= _item.UpgradeLimit ? "Max level" : $"Level {_item.Level}";
        _info.Text = _item.Description;
        if (_item.Level >= _item.UpgradeLimit) _cost.Text = "Maxed";
        else if (_item.BonusUnlockedLevel > _item.Level) _cost.Text = "+1 / FREE!";
        else if (BonusData.TryCost(_item, _item.Level + 1, out double cost)) _cost.Text = $"+1 / {cost:N0} BP";
        else _cost.Text = "Upgrade unavailable";
        string path = $"res://Assets/Bonus/icon/{_item.Id}.png";
        _preview.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }
}
