using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Translation of src.shop.bonusshop.BonusShopEngine + GlassBoard + BPBar.
//
// Coordinates come from the SWF display lists (swfparse.py) and content bounds (swfbounds.py):
//   6824 BonusShopEngine  bg 6823 scale (0.5482,0.5032) @ (-157,-201) tw
//                         glassBoard 6819 scale (0.9479,0.9850) @ (172,112) tw
//                         baby 6809 identity
//   6819 GlassBoard       board 6814 @ 0,0 / btnBack 6818 @ (10291,1175) tw / bpBar 6812 @ (1836,913) tw
//   6812 BPBar            content bounds 67.73 x 39.23 (the badge is scaled down inside the sprite)
//   6814 board shape      bbox (-50.65,-69.05), but only the solid frame (49,69)-(791,652) of the
//                         820x653 export is the visible board - the remainder is drop shadow.
// Stage: Main.as:544 - 720x540 design, scale = stageWidth/720, centred; 16:9 shows a 720x405 band.
public partial class BonusShopScreen : Control
{
    // BonusShopEngine.as:25-33
    private const float MarginLeft = 50f;
    private const float MarginTop = 70f;
    private const float SpacingX = 90f;
    private const float SpacingY = 95f;
    private const int RowLength = 4;

    private const float DesignWidth = 960f;
    private const float DesignHeight = 540f;

    private const float GlassScaleX = 0.9478759765625f;
    private const float GlassScaleY = 0.9850006103515625f;
    private static readonly Vector2 GlassPos = new(652f - 650f * DesignWidth / 720f + (DesignWidth - 720f), 112f / 20f);

    private static readonly Vector2 BoardSize = new(820f, 653f);
    // The PNG is cropped to the shape bbox, so PNG pixel (0,0) is GlassBoard-local bbox-min.
    private const float BoardBoundsMinX = -50.65f, BoardBoundsMinY = -69.05f;
    // solid (non-shadow) frame inside board.png
    private const float BoardSolidX = 49f, BoardSolidY = 69f, BoardSolidW = 742f, BoardSolidH = 583f;

    private static readonly Vector2 BpBarLocal = new(1836f / 20f - 53.618f, 913f / 20f - 18.418f);
    private const float BpBadgeW = 67.73f, BpBadgeH = 39.23f;
    private static readonly Vector2 BackLocal = new(471f, 72f);

    private Control _world = null!;
    private Control _engine = null!;
    private Control _glass = null!;
    private TextureRect _babyArt = null!;
    private readonly List<Texture2D> _babyFrames = new();
    private readonly List<BonusSlot> _slots = new();
    private ColorRect _tooltip = null!;
    private Label _tooltipTitle = null!, _tooltipBody = null!;
    private ShaderMaterial _tooltipMaterial = null!;
    private BonusSlot? _hoveredSlot;
    private Tween? _tooltipFade;
    private BonusSlot? _selected;
    private BonusCard _card = null!;
    private Label _bpValue = null!;
    private bool _verifyMode;
    private AudioStreamPlayer _shopClick = null!;

    private int _babyEnd = 129, _babyNext;
    private int _babyFrame = 75;
    private double _babyAccum;

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

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        bool verify = args.Contains("--verify");
        _verifyMode = verify;
        if (!verify) OriginalMusic.Play(OriginalMusic.Create(this), OriginalMusic.BonusShop);

        SetAnchorsPreset(LayoutPreset.FullRect);

        var viewport = GetViewport().GetVisibleRect().Size;
        float s = Math.Min(viewport.X / DesignWidth, viewport.Y / DesignHeight);
        _world = new Control
        {
            Scale = new Vector2(s, s),
            Position = (viewport - new Vector2(DesignWidth, DesignHeight) * s) * 0.5f
        };
        AddChild(_world);

        var bg = MakeTex("res://Assets/Bonus/bg.png", Vector2.Zero,
                         new Vector2(DesignWidth, DesignHeight), TextureRect.StretchModeEnum.KeepAspectCovered);
        _world.AddChild(bg);
        _engine = new Control();
        _world.AddChild(_engine);

        _glass = new Control { Position = GlassPos + new Vector2(0, 540), Scale = new Vector2(GlassScaleX, GlassScaleY) };
        _engine.AddChild(_glass);
        _glass.AddChild(MakeTex("res://Assets/Bonus/board.png", new Vector2(BoardBoundsMinX, BoardBoundsMinY), BoardSize));
        CreateTween().TweenProperty(_glass, "position", GlassPos, 65 / 60.0)
            .SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);

        _shopClick = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://Assets/Flight/60_sound.Pop1.mp3"), VolumeDb = -4.44f };
        AddChild(_shopClick);

        // BP bar: badge is 67.73x39.23 at GlassBoard-local (91.8-53.618, 45.65-18.418).
        _glass.AddChild(MakeTex("res://Assets/Bonus/bp_badge.png", BpBarLocal, new Vector2(BpBadgeW, BpBadgeH)));

        var numbers = GD.Load<FontFile>("res://Assets/Start/numbers.ttf");
        _bpValue = new Label { Position = BpBarLocal + new Vector2(BpBadgeW + 4f, 2f), Text = PortProfile.State.BonusPoints.ToString("N0") };
        _bpValue.AddThemeFontOverride("font", numbers);
        _bpValue.AddThemeFontSizeOverride("font_size", 32);
        _bpValue.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bpValue.AddThemeConstantOverride("outline_size", 4);
        _glass.AddChild(_bpValue);

        var items = BonusData.Load();
        for (int i = 0; i < items.Count; i++)
        {
            int col = i % RowLength;
            int row = i / RowLength;
            var slot = new BonusSlot
            {
                Position = new Vector2(MarginLeft + col * SpacingX, MarginTop + row * SpacingY)
            };
            slot.Setup(items[i]);
            slot.SignalClick += OnSlotClick;
            slot.HoverIn += OnSlotHoverIn;
            slot.HoverOut += OnSlotHoverOut;
            _glass.AddChild(slot);
            _slots.Add(slot);
        }

        var back = new TextureButton
        {
            TextureNormal = GD.Load<Texture2D>("res://Assets/Bonus/back_up.png"),
            TextureHover = GD.Load<Texture2D>("res://Assets/Bonus/back_over.png"),
            TexturePressed = GD.Load<Texture2D>("res://Assets/Bonus/back_down.png"),
            Position = BackLocal,
            Scale = Vector2.One
        };
        back.Pressed += LeaveBonusShop;
        _glass.AddChild(back);

        // Baby straddles the board's bottom edge, centred.
        for (int i = 1; i <= 166; i++)
            _babyFrames.Add(GD.Load<Texture2D>($"res://Assets/Bonus/baby/{i}.png"));
        const float babyW = 450f, babyH = 562.5f;
        _babyArt = MakeTex("res://Assets/Bonus/baby/75.png",
            new Vector2(509, 219),
            new Vector2(babyW, babyH), TextureRect.StretchModeEnum.KeepAspectCentered);
        _babyArt.ZIndex = 5;
        _engine.AddChild(_babyArt);
        PlayBaby(75, 129, 0);

        _card = new BonusCard
        {
            Position = new Vector2(659, 0)
        };
        _card.LevelChangeRequested += OnLevelChangeRequested;
        _card.Closed += DeselectSlot;
        _world.AddChild(_card);

        _tooltipMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Bonus/tooltip.gdshader") };
        _tooltip = new ColorRect { Visible = false, MouseFilter = MouseFilterEnum.Ignore,
            Material = _tooltipMaterial, ZIndex = 20 };
        _world.AddChild(_tooltip);
        var sans = new SystemFont { FontNames = new[] { "Arial" } };
        _tooltipTitle = TooltipLine(sans, 16);
        _tooltipBody = TooltipLine(sans, 15);

        GuiTick.Signal += OnTick;

        if (verify && args.Contains("--wipe"))
        {
            var ov = new TransitionOverlay();
            AddChild(ov);
            ov.PlayOut(null);
            GetTree().CreateTimer(0.30).Timeout += () =>
            {
                GetViewport().GetTexture().GetImage().SavePng("res://transition-preview.png");
                GD.Print("TRANSITION_WIPE_PASS: 26-tick cover captured");
                GetTree().Quit();
            };
            return;
        }

        if (verify)
        {
            bool selected = args.Contains("--selected");
            if (args.Contains("--hover")) GetTree().CreateTimer(1.2).Timeout += () => ShowTooltip(_slots[13]);
            if (selected) GetTree().CreateTimer(0.4).Timeout += () => OnSlotClick(_slots[Math.Min(11, _slots.Count - 1)]);
            GetTree().CreateTimer(2.0).Timeout += () =>
            {
                GetViewport().GetTexture().GetImage().SavePng(selected ? "res://bonus-selected.png" : "res://bonus-preview.png");
                GD.Print($"BONUS_SHOP_PASS: {_slots.Count} slots, stage {DesignWidth}x{DesignHeight}, " +
                         $"glass origin {GlassPos}");
                GetTree().Quit();
            };
        }
    }

    public override void _Process(double delta)
    {
        GuiTick.Pump(delta);
        if (_tooltip.Visible) PositionTooltip();
    }

    private Label TooltipLine(Font font, int size)
    {
        var line = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        line.AddThemeFontOverride("font", font);
        line.AddThemeFontSizeOverride("font_size", size);
        line.AddThemeColorOverride("font_color", Color.Color8(51, 51, 51));
        _tooltip.AddChild(line);
        return line;
    }

    private void PositionTooltip()
    {
        var mouse = _world.GetLocalMousePosition();
        float left = Mathf.Clamp(mouse.X - _tooltip.Size.X / 2, 0, DesignWidth - _tooltip.Size.X);
        _tooltip.Position = new Vector2(left, Mathf.Max(0, mouse.Y - _tooltip.Size.Y - 10));
        _tooltipMaterial.SetShaderParameter("hook_x", Mathf.Clamp(mouse.X - left, 7, _tooltip.Size.X - 7));
    }

    private void OnTick(double delta)
    {
        _babyAccum += delta;
        while (_babyAccum >= 1.0)
        {
            _babyAccum -= 1.0;
            _babyFrame++;
            if (_babyFrame > _babyEnd)
            {
                if (_babyNext > 0)
                {
                    int next = _babyNext;
                    PlayBaby(next, next == 1 ? 37 : 129, 0);
                }
                else _babyFrame = _babyEnd;
            }
            _babyArt.Texture = _babyFrames[Math.Clamp(_babyFrame - 1, 0, _babyFrames.Count - 1)];
        }
    }

    private void PlayBaby(int start, int end, int next)
    {
        _babyEnd = end; _babyNext = next; _babyFrame = start; _babyAccum = 0;
    }

    private void DeselectSlot()
    {
        if (_selected == null) return;
        _selected.IsSelectedSlot = false;
        _selected = null;
        PlayBaby(47, 67, 100);
    }

    private void OnLevelChangeRequested(int amount)
    {
        if (_selected == null) return;
        var item = _selected.Item;
        int next = Math.Clamp(item.Level + amount, 0, item.UpgradeLimit);
        if (next == item.Level) return;

        if (amount < 0)
        {
            // BonusShopEngine.downgradeItem only lowers the level; it does not refund BP.
            item.Level = next;
        }
        else
        {
            bool free = item.BonusUnlockedLevel > item.Level;
            if (!free)
            {
                if (!BonusData.TryCost(item, item.Level + 1, out double cost) || cost > PortProfile.State.BonusPoints)
                    return;
                PortProfile.State.BonusPoints -= cost;
            }
            item.Level = next;
            item.BonusUnlockedLevel = Math.Max(item.BonusUnlockedLevel, item.Level);
        }

        PortProfile.State.BonusLevels[item.Id] = item.Level;
        PortProfile.State.BonusUnlockedLevels[item.Id] = item.BonusUnlockedLevel;
        PortProfile.Save();
        int overBonusLevel = PortProfile.State.BonusLevels.TryGetValue("bonusOver", out int savedOverLevel) ? savedOverLevel : 0;
        foreach (var slot in _slots)
        {
            slot.Item.OverLevelsAvailable = BonusData.OverLevelsAvailable(slot.Item.Id, overBonusLevel);
            slot.UpdateItem();
        }
        _bpValue.Text = PortProfile.State.BonusPoints.ToString("N0");
    }

    private void LeaveBonusShop()
    {
        PortProfile.ResumeEquipmentShop = PortProfile.BonusShopReturnToEquipmentShop;
        PortProfile.BonusShopReturnToEquipmentShop = false;
        TransitionOverlay.To(this, "res://StartScreen.tscn");
    }

    private void OnSlotClick(BonusSlot slot)
    {
        if (!_verifyMode) _shopClick.Play();
        if (_selected != null) _selected.IsSelectedSlot = false;
        _selected = slot;
        slot.IsSelectedSlot = true;
        _card.Show(slot.Item);
        if (_babyFrame >= 75 && _babyFrame < 137) PlayBaby(137, 158, 1);
    }

    private void OnSlotHoverIn(BonusSlot slot)
    {
        if (!_verifyMode) ShowTooltip(slot);
    }

    private void ShowTooltip(BonusSlot slot)
    {
        _hoveredSlot = slot;
        _tooltipTitle.Text = slot.Item.Name;
        _tooltipBody.Text = slot.Item.Description;
        var titleSize = _tooltipTitle.GetMinimumSize();
        var bodySize = _tooltipBody.GetMinimumSize();
        float width = Math.Max(titleSize.X, bodySize.X);
        _tooltip.Size = new Vector2(width + 10, titleSize.Y + bodySize.Y + 15);
        _tooltipTitle.Position = new Vector2(5, 5);
        _tooltipTitle.Size = new Vector2(width, titleSize.Y);
        _tooltipBody.Position = new Vector2(5, 5 + titleSize.Y);
        _tooltipBody.Size = new Vector2(width, bodySize.Y);
        _tooltipMaterial.SetShaderParameter("tooltip_size", _tooltip.Size);
        PositionTooltip();
        _tooltipFade?.Kill();
        _tooltip.Modulate = new Color(1, 1, 1, 0);
        _tooltip.Show();
        _tooltipFade = CreateTween();
        _tooltipFade.TweenProperty(_tooltip, "modulate:a", 1f, .35)
            .SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
    }

    private void OnSlotHoverOut(BonusSlot slot)
    {
        if (_hoveredSlot != slot) return;
        _hoveredSlot = null;
        _tooltipFade?.Kill();
        _tooltip.Hide();
    }

    public override void _ExitTree() => GuiTick.Signal -= OnTick;
}
