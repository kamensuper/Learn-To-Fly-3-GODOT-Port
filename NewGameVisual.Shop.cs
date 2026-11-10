using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public partial class NewGameVisual
{
    private const string LauncherArt = ArtRoot + "LauncherShop/";
    private const float DrawerTravel = 350;
    private static readonly string[] FreshLaunchers =
    {
        "emptyLauncher", "coil", "explosive", "nuclearReactor", "tiltCannon",
        "slingshot", "cannonPowder", "grenade", "russianRoulette", "ufo"
    };

    private Control? buyDrawer, upgradeDrawer, activeDrawer;
    private Tween? buySlide, upgradeSlide;
    private float buyOffset = DrawerTravel, upgradeOffset = DrawerTravel;
    private Vector2 buyDrift, upgradeDrift;
    private bool coilShop;

    // ShopMainScreen.as:205 selects the slot and opens BuyBar for emptyLauncher.
    // ShopEngine.as:475 purchases into that same slot and opens UpgradeBar.
    private void ConfigureCoilShop(Label launcher, Label cash, Label weight,
        IReadOnlyDictionary<string, OriginalEntry> entries)
    {
        coilShop = true;
        var slot = PortProfile.CurrentSlot;
        var coil = entries["coil"];
        var launcherHighlight = ShopPicture(equipmentLayer!, "slot-highlight", new(98.2f, 217.45f), new(385.65f, 76.95f));
        launcherHighlight.RotationDegrees = -2.01f;
        launcherHighlight.PivotOffset = Vector2.Zero;
        launcherHighlight.Modulate = new Color(1, 1, 1, .2f);
        equipmentLayer!.MoveChild(launcherHighlight, 1);
        var iconSize = GD.Load<Texture2D>(LauncherArt + "icon-coil.png").GetSize() * .6f;
        var icon = ShopPicture(equipmentLayer!, "icon-coil", new Vector2(135.9f, 254.6f) - iconSize / 2, iconSize);
        icon.RotationDegrees = -2;
        var level = ShopText(equipmentLayer!, "", new(199, 253), 17, tint: new Color(1, 1, 1, .6f));
        level.RotationDegrees = -2;
        UpdateSlot();

        var slotArea = ShopHit(equipmentLayer!, new(98.2f, 217.45f), new(385.65f, 76.95f), () =>
        {
            OriginalAudio.PlaySfx(this, OriginalSound.ShopClick);
            if (slot.LauncherId == "emptyLauncher") ShowDrawer(buyDrawer);
            else if (slot.LauncherId == "coil") ShowDrawer(upgradeDrawer);
        });
        slotArea.RotationDegrees = -2.01f;
        slotArea.MouseEntered += () => launcherHighlight.Modulate = Colors.White;
        slotArea.MouseExited += () => launcherHighlight.Modulate = new Color(1, 1, 1, .2f);
        ShopHit(menuLayer!, new(696, 25), new(174, 57), StartCoilFlight);

        buyDrawer = ShopLayer();
        buyDrawer.Name = "LauncherBuyBar";
        buyDrawer.Hide();
        upgradeDrawer = ShopLayer();
        upgradeDrawer.Name = "LauncherUpgradeBar";
        upgradeDrawer.Hide();
        ShopHit(buyDrawer, new(645, 0), new(420, 540), () => { });
        ShopHit(upgradeDrawer, new(655, 0), new(420, 540), () => { });
        ShopPicture(buyDrawer, "buy-bar", new(490, -146));
        ShopPicture(upgradeDrawer, "upgrade-bar", new(499, -157));
        BuildBuyList(buyDrawer, coil, entries, BuyCoil);
        BuildCoilDetails(upgradeDrawer, coil, UpdateSlot, cash);
        ShowLauncherTutorial();

        void UpdateSlot()
        {
            launcher.Text = entries.TryGetValue(slot.LauncherId, out var item) ? item.name : "Empty";
            icon.Visible = slot.LauncherId == "coil";
            level.Text = icon.Visible ? $"Level {slot.LauncherLevel + 1}" : "";
            cash.Text = "$ " + slot.Cash.ToString("N0", CultureInfo.InvariantCulture);
            // Neither emptyLauncher nor Coil contributes mass in Registry.as.
            // ShopMainScreen sums the equipped body and available stage/boost slots.
            if (slot.BodyId == "emptyBody" && slot.StageIds.All(id => id == "emptyStage") &&
                slot.BoostIds.All(id => id == "emptyBoost"))
                weight.Text = "Total weight: " + entries["emptyBody"].Value("statWeight", slot.BodyLevel)
                    .ToString("F1", CultureInfo.InvariantCulture);
        }

        void BuyCoil()
        {
            var price = OriginalStatRules.Cost(coil, 0);
            if (!PortProfile.TryBuyCoil()) return;
            OriginalAudio.PlaySfx(this, OriginalSound.Cash);
            UpdateSlot();
            ShowDrawer(upgradeDrawer);
            var popup = ShopText(equipmentLayer!, "$ " + price.ToString("F0", CultureInfo.InvariantCulture),
                new(194.6f, 243.1f), 25, tint: new Color(0, 1, 0));
            var tween = popup.CreateTween().SetParallel();
            tween.TweenProperty(popup, "position:y", popup.Position.Y - 50, 130 / 60.0)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(popup, "modulate:a", 0f, 130 / 60.0)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Finished += popup.QueueFree;
        }
    }

    private void BuildBuyList(Control drawer, OriginalEntry coil,
        IReadOnlyDictionary<string, OriginalEntry> entries, Action buy)
    {
        // BuyBar.as:79 and ScrollList.as:25 place the 1.1-scaled rows 60 px apart.
        // At 960 px UiScaler's right anchor puts the BuyBar origin at x = 230.
        var clip = new ScrollContainer
        {
            Position = new(694, 139), Size = new(333, 356),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            MouseFilter = MouseFilterEnum.Stop
        };
        drawer.AddChild(clip);
        var rows = new Control { CustomMinimumSize = new(333, FreshLaunchers.Length * 60 + 32) };
        clip.AddChild(rows);
        var stats1 = ShopText(drawer, "", new(687, 90), 15, 127);
        var stats2 = ShopText(drawer, "", new(687, 111), 15, 127);
        var value1 = ShopText(drawer, "", new(813, 90), 15, 52);
        var value2 = ShopText(drawer, "", new(813, 111), 15, 52);
        var normal = GD.Load<Texture2D>(LauncherArt + "buy-row-1.png");
        var over = GD.Load<Texture2D>(LauncherArt + "buy-row-2.png");
        var down = GD.Load<Texture2D>(LauncherArt + "buy-row-3.png");
        var slot = PortProfile.CurrentSlot;
        var visibleRows = new List<(OriginalEntry Item, Control Row)>();
        int index = 0;
        foreach (var item in FreshLaunchers.Select(id => entries[id]).OrderBy(item => OriginalStatRules.Cost(item, 0)))
        {
            var row = new Control { Position = new(3.2f, 31.55f + index++ * 60), Size = new(280.5f, 55.11f) };
            rows.AddChild(row);
            visibleRows.Add((item, row));
            var background = new TextureRect
            {
                Texture = normal, Size = new(284, 59), Position = new(-1.65f, -1.65f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddChild(background);
            var iconPath = LauncherArt + "icon-" + item.id + ".png";
            if (ResourceLoader.Exists(iconPath))
            {
                var texture = GD.Load<Texture2D>(iconPath);
                Vector2 iconSize = texture.GetSize() * .495f;
                ShopPicture(row, "icon-" + item.id, new Vector2(32.4f, 27.5f) - iconSize / 2, iconSize);
            }
            double price = OriginalStatRules.Cost(item, 0);
            ShopText(row, item.name, new(58.6f, 1.9f), 20);
            ShopText(row, "$ " + price.ToString("F0", CultureInfo.InvariantCulture), new(58.6f, 30.5f), 16,
                tint: new Color(0, .82f, 0));
            var input = ShopHit(row, Vector2.Zero, row.Size, () =>
            {
                if (item.id == "coil" && slot.Cash >= price && activeDrawer == drawer) buy();
            });
            if (slot.Cash < price) row.Modulate = new Color(.6f, .6f, .6f);
            input.MouseEntered += () =>
            {
                if (slot.Cash >= price) background.Texture = over;
                if (item != coil) return;
                stats1.Text = "Power";
                stats2.Text = "Max Lift";
                value1.Text = coil.Value("statPower", 0).ToString("G", CultureInfo.InvariantCulture);
                value2.Text = coil.stats["statPowerAtWeightMult"][0][2].ToString("G", CultureInfo.InvariantCulture);
            };
            input.MouseExited += () =>
            {
                background.Texture = normal;
                if (item != coil) return;
                stats1.Text = stats2.Text = value1.Text = value2.Text = "";
            };
            input.GuiInput += evt =>
            {
                if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Left } clickEvent && slot.Cash >= price)
                    background.Texture = clickEvent.Pressed ? down : over;
            };
        }
        int sortMethod = 0;
        var sortTexture = ShopPicture(drawer, "sort-button-1", new(698.2f, 65.55f));
        var sortHit = ShopHit(drawer, sortTexture.Position, new(162, 24), () =>
        {
            sortMethod = (sortMethod + 1) % 3;
            sortTexture.Texture = GD.Load<Texture2D>(LauncherArt + $"sort-button-{sortMethod + 1}.png");
            var ordered = sortMethod switch
            {
                1 => visibleRows.OrderBy(entry => entry.Item.name, StringComparer.Ordinal),
                2 => visibleRows.OrderBy(entry => Array.IndexOf(FreshLaunchers, entry.Item.id)),
                _ => visibleRows.OrderBy(entry => OriginalStatRules.Cost(entry.Item, 0))
            };
            int rowIndex = 0;
            foreach (var entry in ordered)
                entry.Row.Position = new(3.2f, 31.55f + rowIndex++ * 60);
            clip.ScrollVertical = 0;
        });
        sortHit.MouseEntered += () => sortTexture.Modulate = new Color(.5f, 1, .5f);
        sortHit.MouseExited += () => sortTexture.Modulate = Colors.White;
    }

    private void BuildCoilDetails(Control drawer, OriginalEntry coil, Action updateSlot, Label cash)
    {
        // Symbol 1910, frame label coil (95), is UpgradeBar.ad for this item.
        ShopPicture(drawer, "coil-ad", new(705, 13), new(268, 163.5f));
        ShopText(drawer, coil.name, new(696, 173), 29, 242);
        var slot = PortProfile.CurrentSlot;
        var levelLabel = ShopText(drawer, slot.LauncherLevel >= coil.MaxLevelInt ? "Max level" : $"Level {slot.LauncherLevel + 1}", new(705, 203), 20, 220, new Color(1, 1, 1, .6f));
        ShopText(drawer, coil.descriptions.FirstOrDefault()?.text ?? "", new(720, 244), 17, 205);
        ShopText(drawer, "Power", new(696, 377), 15, 127);
        ShopText(drawer, "Max Lift", new(696, 397), 15, 127);
        var powerLabel = ShopText(drawer, coil.Value("statPower", slot.LauncherLevel).ToString("G", CultureInfo.InvariantCulture), new(823, 377), 15, 52);
        ShopText(drawer, coil.stats["statPowerAtWeightMult"][0][2].ToString("G", CultureInfo.InvariantCulture),
            new(823, 397), 15, 52);

        var costLabel = ShopText(drawer, slot.LauncherLevel < coil.MaxLevelInt ? "$ " + OriginalStatRules.Cost(coil, slot.LauncherLevel + 1).ToString("N0", CultureInfo.InvariantCulture) : "", new(720, 435), 20, 100, new Color(0, 1, 0));
        ShopHit(drawer, new(825, 430), new(95, 36), () =>
        {
            if (slot.LauncherLevel >= coil.MaxLevelInt) return;
            double price = OriginalStatRules.Cost(coil, slot.LauncherLevel + 1);
            if (!PortProfile.TryUpgradeCoil()) return;
            OriginalAudio.PlaySfx(this, OriginalSound.Cash);
            updateSlot();
            levelLabel.Text = slot.LauncherLevel >= coil.MaxLevelInt ? "Max level" : $"Level {slot.LauncherLevel + 1}";
            powerLabel.Text = coil.Value("statPower", slot.LauncherLevel).ToString("G", CultureInfo.InvariantCulture);
            costLabel.Text = slot.LauncherLevel < coil.MaxLevelInt ? "$ " + OriginalStatRules.Cost(coil, slot.LauncherLevel + 1).ToString("N0", CultureInfo.InvariantCulture) : "";
            var popup = ShopText(equipmentLayer!, "$ " + price.ToString("F0", CultureInfo.InvariantCulture),
                new(194.6f, 243.1f), 25, tint: new Color(0, 1, 0));
            var tween = popup.CreateTween().SetParallel();
            tween.TweenProperty(popup, "position:y", popup.Position.Y - 50, 130 / 60.0)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(popup, "modulate:a", 0f, 130 / 60.0)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Finished += popup.QueueFree;
        });
        ShopText(drawer, "+", new(865, 433), 24, 30);

        // Stock btnBack belongs to the rasterized UpgradeBar. Escape and a click
        // in the left shop screen are the other stock ways to close the bars.
        ShopHit(drawer, new(714, 481), new(94.15f, 38.4f), () => ShowDrawer(null));
    }

    private void ShowLauncherTutorial()
    {
        if (PortProfile.State.TutorialBuyLauncher) return;
        PortProfile.State.TutorialBuyLauncher = true;
        PortProfile.Save();
        var panel = ShopLayer();
        panel.Name = "LauncherTutorial";
        var tutorialArt = GD.Load<Texture2D>(LauncherArt + "launcher-tutorial.png");
        Art(new AtlasTexture { Atlas = tutorialArt, Region = new Rect2(0, 110, 692, 366) }, new(18, 192))
            .Reparent(panel, false);
        // The TutorialBox arrow has its own rightward UiScaler anchor.
        Art(new AtlasTexture { Atlas = tutorialArt, Region = new Rect2(0, 0, 110, 110) }, new(121, 82))
            .Reparent(panel, false);
        var tutorialOk = ShopHit(panel, new(591, 478.85f), new(107, 52.1f), () =>
        {
            var tween = panel.CreateTween();
            tween.TweenProperty(panel, "position:y", 720, 35 / 60.0)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Finished += panel.QueueFree;
        });
        tutorialOk.Name = "ConfirmLauncherTutorial";
        panel.Position = new(0, 720);
        panel.CreateTween().TweenProperty(panel, "position:y", 0, 65 / 60.0)
            .SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
    }

    private void StartCoilFlight()
    {
        if (!coilShop || changing || TransitionOverlay.IsActive(GetTree())) return;
        if (!CoilStoryRun.CanLaunch)
        {
            if (PortProfile.CurrentSlot.LauncherId == "emptyLauncher")
            {
                OriginalAudio.PlaySfx(this, OriginalSound.ShopClick);
                ShowDrawer(buyDrawer);
            }
            return;
        }
        OriginalAudio.PlaySfx(this, OriginalSound.MediumClick);
        CoilStoryRun.Launch(GetTree());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!coilShop || changing || TransitionOverlay.IsActive(GetTree())) return;
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Enter && !key.AltPressed) StartCoilFlight();
            else if (key.Keycode == Key.Escape) ShowDrawer(null);
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } &&
            GetGlobalMousePosition().X <= GetViewportRect().Size.X - 280)
            ShowDrawer(null);
    }

    private void ShowDrawer(Control? next)
    {
        if (next == activeDrawer) return;
        activeDrawer = next;
        if (buyDrawer != null) Slide(buyDrawer, buyDrawer == next, true);
        if (upgradeDrawer != null) Slide(upgradeDrawer, upgradeDrawer == next, false);
    }

    private void Slide(Control drawer, bool show, bool buying)
    {
        var previous = buying ? buySlide : upgradeSlide;
        previous?.Kill();
        if (show) drawer.Show();
        if (!drawer.Visible) return;
        float from = buying ? buyOffset : upgradeOffset;
        var slide = drawer.CreateTween();
        slide.TweenMethod(Callable.From<float>(offset =>
        {
            if (buying) buyOffset = offset;
            else upgradeOffset = offset;
            drawer.Position = new Vector2(offset, 0) + (buying ? buyDrift : upgradeDrift);
        }), from, show ? 0 : DrawerTravel, 40 / 60.0)
            .SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
        if (!show) slide.Finished += drawer.Hide;
        if (buying) buySlide = slide;
        else upgradeSlide = slide;
    }

    private void MoveLauncherDrawers(Vector2 offset, float blend)
    {
        buyDrift = buyDrift.Lerp(offset * new Vector2(.2f, 2.6f), blend);
        upgradeDrift = upgradeDrift.Lerp(offset * 1.2f, blend);
        if (buyDrawer != null) buyDrawer.Position = new Vector2(buyOffset, 0) + buyDrift;
        if (upgradeDrawer != null) upgradeDrawer.Position = new Vector2(upgradeOffset, 0) + upgradeDrift;
    }

    private void ResetLauncherDrawers()
    {
        buySlide?.Kill();
        upgradeSlide?.Kill();
        buyDrawer = upgradeDrawer = activeDrawer = null;
        buySlide = upgradeSlide = null;
        buyOffset = upgradeOffset = DrawerTravel;
        buyDrift = upgradeDrift = Vector2.Zero;
        coilShop = false;
    }

    private static Control ShopHit(Control parent, Vector2 position, Vector2 size, Action clickAction)
    {
        var hit = new Control { Position = position, Size = size, MouseFilter = MouseFilterEnum.Stop };
        parent.AddChild(hit);
        bool pressed = false;
        hit.GuiInput += evt =>
        {
            if (evt is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;
            hit.AcceptEvent();
            if (!mouse.Pressed && pressed && new Rect2(Vector2.Zero, hit.Size).HasPoint(mouse.Position))
                clickAction();
            pressed = mouse.Pressed;
        };
        return hit;
    }

    private TextureRect ShopPicture(Control parent, string file, Vector2 position, Vector2? size = null)
    {
        var picture = Art(GD.Load<Texture2D>(LauncherArt + file + ".png"), position);
        picture.Reparent(parent, false);
        if (size is { } target)
        {
            picture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            picture.Size = target;
        }
        return picture;
    }

    private Label ShopText(Control parent, string value, Vector2 position, int fontSize, int width = 0, Color? tint = null)
    {
        var label = Text(value, position, fontSize, width, tint, outline: 3);
        label.Reparent(parent, false);
        return label;
    }
}
