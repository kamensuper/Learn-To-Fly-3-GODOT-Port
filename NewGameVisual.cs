using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class NewGameVisual : Control
{
    private const string ArtRoot = "res://Assets/NewGame/";
    private readonly List<MouseBounce2> decorations = new();
    private Control panel = null!;
    private Label info = null!;
    private Label cards = null!;
    private bool changing;
    private int hoveredMode;
    private Control? equipmentLayer, menuLayer, sardineLayer;
    private Vector2 shopMouseOrigin;
    private bool shopAnimationReady;
    private readonly List<(ShaderMaterial Material, MouseBounce2 Bounce)> modeColors = new();

    public override void _Ready()
    {
        ShowModes();
    }

    private void ShowModes()
    {
        changing = false;
        ResetPanel();
        var layout = GD.Load<Texture2D>(ArtRoot + "mode-layout.png");
        // Keep the title and Back display objects from symbol 3345. The remaining
        // baked layers are replaced by the four runtime frames of symbol 3342.
        Art(new AtlasTexture { Atlas = layout, Region = new Rect2(0, 0, 517, 75) }, new(222, 19));
        var back = Art(new AtlasTexture { Atlas = layout, Region = new Rect2(210, 466, 94, 38) }, new(432, 485));
        Decorate(back, () => Leave(ReturnToMenu));

        info = Text("", new(228, 353), 20, 504);
        cards = Text("", new(228, 415), 20, 504);
        cards.AddThemeColorOverride("font_color", new Color(.8f, .8f, .8f));
        info.AddThemeConstantOverride("line_spacing", 2);
        cards.AddThemeConstantOverride("line_spacing", 2);
        var profile = PortProfile.State;
        Mode(1, new(289, 111), "Story Mode", false, "Earn money and get better upgrades to prove that\npenguins can make it to space!", "(5 to 10 cards per game)");
        Mode(2, new(289, 232), "Classic Mode", !profile.ModeUnlockedClassic, "No obstacles, the goal is to fly far away horizontally. The original Learn to Fly flavor.", "(5 cards per game)");
        Mode(3, new(486, 111), "Payload Mode", !profile.ModeUnlockedPayload, "Sure you can fly to space, but can you make it with extra weight?", "(6 cards per game)");
        Mode(4, new(486, 232), "Sandbox", !profile.ModeUnlockedSandbox, "Adjust every game constant as you please, crash your browser to your heart's content.", "(Cannot earn cards in this mode)\n(Cannot earn most medals in this mode)");

        Enter(panel, new Vector2(0, 400), 30);
    }

    private void Mode(int frame, Vector2 position, string mode, bool locked, string description, string reward)
    {
        var art = Art(GD.Load<Texture2D>($"{ArtRoot}mode-{frame}.png"), position);
        // The extracted port has the four stock mode frames but not the SWF's
        // locked frame 5. Keep the authentic frame, visibly dim it, and prevent
        // selection until the corresponding stock profile flag is unlocked.
        if (locked) art.Modulate = new Color(.55f, .55f, .55f, 1);
        var bounce = Decorate(art, () =>
        {
            if (locked) return;
            Leave(() =>
            {
                var profile = PortProfile.State;
                if (mode == "Story Mode" && (profile.Medals.GetValueOrDefault("medalStoryMode") >= 1 || profile.ModeUnlockedSandbox))
                    ShowGoalMenu();
                else
                    StartSelectedGame(mode);
            });
        });
        bounce.Enabled = !locked;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ArtRoot + "mode-state.gdshader") };
        art.Material = material;
        modeColors.Add((material, bounce));
        bounce.SignalOver += _ =>
        {
            if (locked) return;
            hoveredMode = frame;
            info.Text = description;
            cards.Text = PortProfile.State.ModeUnlockedClassic || PortProfile.State.ModeUnlockedPayload ? reward : "";
        };
        bounce.SignalOut += _ =>
        {
            if (hoveredMode != frame) return;
            hoveredMode = 0;
            info.Text = "";
            cards.Text = "";
        };
    }

    private void Leave(Action next, float distance = 400)
    {
        if (changing) return;
        changing = true;
        OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);
        foreach (var decoration in decorations) decoration.Enabled = false;
        var tween = CreateTween();
        tween.TweenProperty(panel, "position", new Vector2(0, distance), .5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(next));
    }

    public override void _Process(double delta)
    {
        bool pressed = Input.IsMouseButtonPressed(MouseButton.Left);
        foreach (var (material, bounce) in modeColors)
        {
            bool over = bounce.MouseIsOver;
            material.SetShaderParameter("rgb_multiplier", !over ? 1f : pressed ? 233f / 256 : .75f);
            material.SetShaderParameter("rgb_offset", over && !pressed ? 64f / 255 : 0f);
        }
        if (!shopAnimationReady) return;
        Vector2 mouse = GetGlobalMousePosition() - shopMouseOrigin;
        Vector2 offset = mouse * new Vector2(10f / 720, 7f / 720);
        float blend = 1 - Mathf.Pow(.95f, (float)delta * 60);
        equipmentLayer!.Position = equipmentLayer.Position.Lerp(offset * .8f, blend);
        menuLayer!.Position = menuLayer.Position.Lerp(offset * new Vector2(1.5f, 1.9f), blend);
        sardineLayer!.Position = sardineLayer.Position.Lerp(offset * new Vector2(1.9f, 3.1f), blend);
        MoveLauncherDrawers(offset, blend);
    }

    private void Enter(Control target, Vector2 offset, int ticks)
    {
        Vector2 destination = target.Position;
        target.Position += offset;
        CreateTween().TweenProperty(target, "position", destination, ticks / 60.0)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void ReturnToMenu() => GetTree().ChangeSceneToFile("res://StartScreen.tscn");

    private void StartSelectedGame(string mode, double goal = 200000)
    {
        PortProfile.StartNewGame(PortProfile.State.CurrentSlotIndex, mode, goal);
        if (mode == "Story Mode") CoilStoryIntro.Play(this, () => ShowShop(true));
        else ShowShop(false);
    }

    private void ShowGoalMenu()
    {
        // SWF symbols 3352 and 3351. NewGameGoalMenu populates these four
        // buttons at runtime and adds the original in/out TweenPos motions.
        changing = false;
        ResetPanel();
        Art(GD.Load<Texture2D>(LauncherArt + "goal-layout.png"), new(259, 27));
        var back = new Control { Position = new(435, 485), Size = new(90, 36), MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(back);
        Decorate(back, () => Leave(ShowModes));
        int[] goals = { 200000, 500000, 1000000, 4000000 };
        int[] rewards = { 5, 6, 7, 10 };
        float[] sourceY = { 126.6f, 208.55f, 290.5f, 372.55f };
        var blank = GD.Load<Texture2D>(LauncherArt + "goal-button.png");
        for (int i = 0; i < goals.Length; i++)
        {
            int goal = goals[i];
            string caption = goal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + (i == 0 ? " (default)" : "");
            var button = Art(blank, new(299, sourceY[i] - 28.45f));
            button.Scale = Vector2.One * .8769989f;
            var goalLabel = Text(caption, new(0, 8), 30, 410, outline: 5);
            var cardsLabel = Text(rewards[i] + " cards per game", new(0, 47), 20, 410,
                fill: new Color("#cccccc"), outline: 4);
            goalLabel.Reparent(button, false);
            cardsLabel.Reparent(button, false);
            Decorate(button, () => Leave(() => StartSelectedGame("Story Mode", goal)));
        }
        Enter(panel, new Vector2(0, 400), 30);
    }

    public void OpenCurrentShop() => ShowShop(PortProfile.CurrentSlot.GameMode == "Story Mode");

    private void ShowShop(bool story)
    {
        if (GetParent() is StartScreen start) start.PlayEquipmentShopMusic();
        changing = false;
        ResetPanel();
        Art(GD.Load<Texture2D>(ArtRoot + "shop-background.png"), new(-25, -12));
        Art(GD.Load<Texture2D>($"{ArtRoot}shop-{(story && !PortProfile.State.TutorialStages ? 4 : 1)}.png"), new(-323, -416));
        equipmentLayer = ShopLayer();
        var menu = Art(GD.Load<Texture2D>(ArtRoot + "shop-menu.png"), new(631, -145));
        menuLayer = ShopLayer();
        var sardines = Art(GD.Load<Texture2D>(ArtRoot + "shop-sardines.png"), new(560, 256));
        // Slot.setItem uses the authored box even when ItemIcon's empty frame is blank.
        Art(GD.Load<Texture2D>(ArtRoot + "slot-box.png"), new(98, 216));
        var launcher = Text("Empty", new(179, 223), 24);
        launcher.RotationDegrees = -2;
        var slot = PortProfile.CurrentSlot;
        var entries = OriginalCatalog.Load().ToDictionary(entry => entry.id, entry => entry);
        string launcherName = entries.TryGetValue(slot.LauncherId, out var launcherEntry) ? launcherEntry.name : "Empty";
        var cash = Text("$ " + slot.Cash.ToString("N0"), new(94, 43), 32);
        Text("DAY " + slot.Day, new(449, 29), 24, fill: new Color("#e5e5e5"));
        Text(PortProfile.State.BonusPoints.ToString("N0") + " BP", new(467, 53), 20, fill: new Color("#999999"), stroke: new Color("#1b1b1b"));
        double weight = 0;
        AddWeight(slot.BodyId, slot.BodyLevel);
        AddWeight(slot.LauncherId, slot.LauncherLevel);
        var body = entries.GetValueOrDefault(slot.BodyId);
        int stageCount = body?.stats.ContainsKey("statStageSlots") == true ? (int)body.Value("statStageSlots", slot.BodyLevel) : 0;
        int boostCount = body?.stats.ContainsKey("statBoostSlots") == true ? (int)body.Value("statBoostSlots", slot.BodyLevel) : 0;
        for (int i = 0; i < Math.Min(stageCount, slot.StageIds.Length); i++) AddWeight(slot.StageIds[i], slot.StageLevels[i]);
        for (int i = 0; i < Math.Min(boostCount, slot.BoostIds.Length); i++) AddWeight(slot.BoostIds[i], slot.BoostLevels[i]);
        var totalWeight = Text("Total weight: " + Math.Max(1, weight).ToString("N1"), new(225, 497), 20, fill: new Color("#e5e5e5"));
        launcher.Text = launcherName;
        var sardineCount = Text(PortProfile.State.Sardines.ToString("N0"), new(777, 478), 36, outline: 7);
        sardineLayer = ShopLayer();
        foreach (Node child in panel.GetChildren())
        {
            if (child is not Control element || element == panel.GetChild(0) ||
                element == equipmentLayer || element == menuLayer || element == sardineLayer) continue;
            element.Reparent(element == menu ? menuLayer :
                element == sardines || element == sardineCount ? sardineLayer : equipmentLayer, false);
        }
        Enter(equipmentLayer, new Vector2(-70, 35), 25);
        Enter(menuLayer, new Vector2(20, -10), 25);
        Enter(sardineLayer, new Vector2(30, 15), 25);
        var settled = CreateTween();
        settled.TweenInterval(25 / 60.0);
        settled.Finished += EnableShopMotion;
        ShopHit(menuLayer, new(712, 285), new(141, 38), () =>
        {
            OriginalAudio.PlaySfx(this, OriginalSound.MediumClick);
            PortProfile.Save();
            TransitionOverlay.To(this, "res://StartScreen.tscn");
        });
        ShopHit(menuLayer, new(719, 86), new(141, 34), () =>
        {
            OriginalAudio.PlaySfx(this, OriginalSound.MediumClick);
            PortProfile.BonusShopReturnToEquipmentShop = true;
            TransitionOverlay.To(this, "res://BonusShop.tscn");
        });
        if (story)
            ConfigureCoilShop(launcher, cash, totalWeight, entries);

        void AddWeight(string id, int level)
        {
            if (!entries.TryGetValue(id, out var entry) || !entry.stats.ContainsKey("statWeight")) return;
            try { weight += entry.Value("statWeight", level); }
            catch (ArgumentOutOfRangeException) { }
            catch (NotSupportedException) { }
        }
    }

    private void EnableShopMotion()
    {
        shopMouseOrigin = GetGlobalMousePosition();
        shopAnimationReady = true;
    }

    private Control ShopLayer()
    {
        var layer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(layer);
        return layer;
    }

    private TextureRect Art(Texture2D texture, Vector2 position)
    {
        var art = new TextureRect { Texture = texture, Position = position, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(art);
        art.Size = texture.GetSize();
        art.PivotOffset = art.Size / 2;
        return art;
    }

    private Label Text(string value, Vector2 position, int size, int width = 0, Color? fill = null, Color? stroke = null, int outline = 5)
    {
        var label = new Label { Text = value, Position = position, Size = new(width, 0),
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = width > 0 ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off };
        label.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Start/numbers.ttf"));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", fill ?? Colors.White);
        label.AddThemeColorOverride("font_outline_color", stroke ?? Colors.Black);
        label.AddThemeConstantOverride("outline_size", outline);
        panel.AddChild(label);
        return label;
    }

    private MouseBounce2 Decorate(Control target, Action action)
    {
        var bounce = new MouseBounce2(target);
        bounce.SignalClick += _ => action();
        decorations.Add(bounce);
        return bounce;
    }

    private void ResetPanel()
    {
        shopAnimationReady = false;
        ResetLauncherDrawers();
        ClearDecorations();
        if (IsInstanceValid(panel))
        {
            RemoveChild(panel);
            panel.QueueFree();
        }
        panel = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(panel);
    }

    private void ClearDecorations()
    {
        foreach (var decoration in decorations) decoration.Destroy();
        decorations.Clear();
        modeColors.Clear();
    }

    public override void _ExitTree() => ClearDecorations();
}
