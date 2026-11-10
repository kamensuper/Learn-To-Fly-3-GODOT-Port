using Godot;
using System;

// Translation of src.shop.bonusshop.Slot.
// Keeps the original state rules: level 0 dims to a locked look, a maxed item shows OverlayMaxed,
// selection forces full saturation and adds the yellow glow.
public partial class BonusSlot : Control
{
    public BonusItemState Item = null!;
    public event Action<BonusSlot>? SignalClick;
    public event Action<BonusSlot>? HoverIn;
    public event Action<BonusSlot>? HoverOut;

    private TextureRect _frame = null!;
    private TextureRect? _icon;
    private TextureRect _maxed = null!;
    private Control _selection = null!;
    private ShaderMaterial _colorFilter = null!;
    private bool _mouseIsOver;
    private bool _isSelectedSlot;

    public bool IsSelectedSlot
    {
        get => _isSelectedSlot;
        set { _isSelectedSlot = value; UpdateItem(); }
    }

    private static TextureRect MakeTex(string path, Vector2 pos, Vector2 size,
        TextureRect.StretchModeEnum stretch = TextureRect.StretchModeEnum.Scale)
    {
        var t = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = stretch,
            Position = pos,
            MouseFilter = MouseFilterEnum.Ignore
        };
        t.Texture = GD.Load<Texture2D>(path);
        t.Size = size;
        return t;
    }

    public void Setup(BonusItemState item)
    {
        Item = item;
        CustomMinimumSize = new Vector2(80, 80);
        Size = new Vector2(80, 80);
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = false;

        _frame = MakeTex("res://Assets/Bonus/slot.png", Vector2.Zero, new Vector2(80, 80));
        AddChild(_frame);

        var iconPath = $"res://Assets/Bonus/icon/{item.Id}.png";
        if (ResourceLoader.Exists(iconPath))
        {
            _icon = MakeTex(iconPath, Vector2.Zero, new Vector2(80, 80),
                TextureRect.StretchModeEnum.KeepAspectCentered);
            AddChild(_icon);
        }

        _maxed = MakeTex("res://Assets/Bonus/overlay_maxed.png", new Vector2(-1, 21), new Vector2(85, 42),
            TextureRect.StretchModeEnum.KeepAspectCentered);
        _maxed.Texture = new AtlasTexture { Atlas = _maxed.Texture, Region = new Rect2(7, 14, 86, 43) };
        _maxed.Visible = false;
        AddChild(_maxed);

        _colorFilter = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Bonus/slot-state.gdshader") };
        _frame.Material = _colorFilter;
        if (_icon != null) _icon.Material = _colorFilter;
        _maxed.Material = _colorFilter;
        _selection = new ColorRect { Position = new Vector2(-7, -7), Size = Size + new Vector2(14, 14),
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Bonus/slot-glow.gdshader") } };
        AddChild(_selection);
        UpdateItem();
    }

    public void UpdateItem()
    {
        // Slot.as:44-64
        float saturation = 1f, brightness = 1f;
        float tintAlpha = 0f;

        _maxed.Visible = Item.Level > 0 && Item.Level >= Item.UpgradeLimit;
        if (Item.Level == 0)
        {
            if (Item.BonusUnlockedLevel > 0) { saturation = 0.7f; brightness = 0.6f; }
            else saturation = 0f;
        }
        else
        {
            saturation = 1f;

        }

        if (_isSelectedSlot) { saturation = 1f; brightness = 1.1f; tintAlpha = 0.4f; }
        else if (_mouseIsOver) { saturation = 1f; brightness = 1.1f; tintAlpha = 0.2f; }

        _colorFilter.SetShaderParameter("saturation", saturation);
        _colorFilter.SetShaderParameter("brightness", brightness);
        _colorFilter.SetShaderParameter("tint_amount", tintAlpha);
        _selection.Visible = _isSelectedSlot;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            SignalClick?.Invoke(this);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseEnter)
        {
            _mouseIsOver = true;
            UpdateItem();
            HoverIn?.Invoke(this);
        }
        else if (what == NotificationMouseExit)
        {
            _mouseIsOver = false;
            UpdateItem();
            HoverOut?.Invoke(this);
        }
    }
}



