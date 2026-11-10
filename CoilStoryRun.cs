using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class CoilStoryRun : Control
{
    private const string ProgressScene = "res://CoilStoryProgress.tscn";
    private const string FlightScene = "res://CoilStoryFlight.tscn";
    private const string StartScene = "res://StartScreen.tscn";
    private const string Art = "res://Assets/CoilStory/";
    private const int ReleaseFrame = 103;
    private const int FinalCoilFrame = 164;
    private const float GroundTileWidth = 2000;
    private const int MaxSkyFrame = 500;
    private static readonly Vector2 ViewSize = new(960, 540);
    private static readonly Vector2 AbortOrigin = new(867f, 0f);
    private readonly Texture2D?[] coilFrames = new Texture2D?[FinalCoilFrame + 1];
    private readonly Queue<double> cameraOffsets = new(Enumerable.Repeat(80d, 30));
    private readonly List<MouseBounce2> progressButtons = new();
    private readonly List<Sprite2D> groundTiles = new();
    private readonly List<(Sprite2D Art, Vector2 Pos, float Speed, float Scale)> flightClouds = new();
    private CoilStoryPhysics physics = null!;
    private CoilStoryObstacles obstacles = null!;
    private Node2D moving = null!, penguinPivot = null!, hud = null!, timerPanel = null!;
    private Node2D altTapeNode = null!;
    private readonly Label[] altLabels = new Label[5];
    private CpuParticles2D windParticles = null!;
    private Sprite2D sky = null!, coil = null!, speedNeedle = null!, dragGauge = null!, abort = null!,
        abortWarning = null!, abortTimer = null!, speaker = null!, optionsButton = null!;
    private Label timeLabel = null!;
    private AudioStreamPlayer shopMusic = null!;
    private OriginalFlightMusicSet flightMusic = null!;
    private double[][] tracer = null!;
    private double accumulator, sinceTakeoff, abortHeld;
    private Vector2 smoothedLook = Vector2.Zero;
    private Vector2 smoothedCamPos = Vector2.Zero;
    private bool camInitialized;
    private float zoom = 1;
    private int coilFrame = 1, skyFrame = 1, dragFrame = 1, slotIndex, day;
    private bool ending, goingAway, mouseAbort;
    private bool verifyCapture;
    private int captureTicks;

    [Export] public bool Progress { get; set; }

    public static bool CanLaunch
    {
        get
        {
            var slot = PortProfile.CurrentSlot;
            return slot.GameMode == "Story Mode" && slot.BodyId == "emptyBody" && slot.BodyLevel == 0 &&
                slot.LauncherId == "coil" && slot.LauncherLevel >= 0 && slot.RudderId == "simpleRudder" &&
                slot.StageIds.All(id => id == "emptyStage") && slot.BoostIds.All(id => id == "emptyBoost");
        }
    }

    public static void Launch(SceneTree tree)
    {
        if (CanLaunch && tree.CurrentScene is Node current)
            TransitionOverlay.To(current, ProgressScene);
    }

    public override void _Ready()
    {
        verifyCapture = Array.Exists(OS.GetCmdlineUserArgs(), s => s == "--capture-coil-flight");
        if (!CanLaunch)
        {
            // If running directly in test/capture mode, ensure current slot has Coil equipped
            if (verifyCapture || PortProfile.CurrentSlot.GameMode != "Story Mode" || PortProfile.CurrentSlot.LauncherId != "coil")
            {
                if (PortProfile.CurrentSlot.GameMode != "Story Mode")
                    PortProfile.StartNewGame(PortProfile.State.CurrentSlotIndex, "Story Mode");
                PortProfile.CurrentSlot.LauncherId = "coil";
                PortProfile.CurrentSlot.LauncherLevel = 0;
            }

            if (!CanLaunch)
            {
                PortProfile.ResumeEquipmentShop = true;
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, StartScene);
                return;
            }
        }
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        slotIndex = PortProfile.State.CurrentSlotIndex;
        day = PortProfile.CurrentSlot.Day;
        if (Progress) BuildProgress();
        else BuildFlight();
        optionsButton = Sprite(this, GD.Load<Texture2D>("res://Assets/Start/options.png"), new(7f, 3f));
        optionsButton.Scale = new(0.85f, 0.85f);
        optionsButton.ZIndex = 10;
        speaker = Sprite(this, OriginalAudio.SpeakerTexture(), new(46.5f, 10.15f));
        speaker.ZIndex = 10;
    }

    private void BuildProgress()
    {
        shopMusic = OriginalMusic.Create(this);
        OriginalMusic.Play(shopMusic, OriginalMusic.Shop);
        // StoryProgressScreen symbol 2098. BtnScreen is exported in its offline state.
        Sprite(this, Asset("Progress"), Vector2.Zero);
        ProgressButton("Takeoff", new(625.85f, 54.9f), new(-96.1f, -20.45f, 212, 44), Takeoff);
        ProgressButton("ProgressBack", new(620.4f, 362.35f), new(-70.6f, -9.2f, 142, 17), ReturnToShop);
    }

    private void ProgressButton(string asset, Vector2 anchor, Rect2 bounds, Action action)
    {
        var art = new TextureRect
        {
            Texture = new AtlasTexture { Atlas = Asset(asset), Region = asset == "Takeoff"
                ? new Rect2(108, 62, 212, 44) : new Rect2(3, 7, 142, 17) },
            Position = anchor + bounds.Position, Size = bounds.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore,
            PivotOffset = -bounds.Position
        };
        AddChild(art);
        var bounce = new MouseBounce2(art);
        bounce.SignalClick += _ => action();
        progressButtons.Add(bounce);
    }

    private void Takeoff()
    {
        if (goingAway || TransitionOverlay.IsActive(GetTree())) return;
        goingAway = true;
        OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);
        TransitionOverlay.To(this, FlightScene);
    }

    private void ReturnToShop()
    {
        if (goingAway) return;
        goingAway = true;
        PortProfile.ResumeEquipmentShop = true;
        TransitionOverlay.To(this, StartScene);
    }

    private void BuildFlight()
    {
        physics = new CoilStoryPhysics(PortProfile.CurrentSlot);
        using var file = Godot.FileAccess.Open(Art + "coil-tracer.json", Godot.FileAccess.ModeFlags.Read);
        tracer = JsonSerializer.Deserialize<double[][]>(file.GetAsText())!;
        sky = Sprite(this, SkyFrame(1), new(-81.5f, -21.5f));
        moving = new Node2D();
        AddChild(moving);
        for (int i = -2; i <= 2; i++)
            groundTiles.Add(Sprite(moving, Asset("Ground"), new(i * GroundTileWidth - 1000.5f, 668.7f)));

        // Clouds (Clouds.as): multi-layer drifting atmospheric clouds
        var cloudTex = GD.Load<Texture2D>("res://Assets/Start/cloud.png");
        var cloudRng = new RandomNumberGenerator();
        cloudRng.Seed = 2026;
        for (int i = 0; i < 24; i++)
        {
            float scale = cloudRng.RandfRange(0.7f, 2.8f);
            float cx = cloudRng.RandfRange(-1000, 1000);
            float cy = 670 - cloudRng.RandfRange(-200, 1400);
            var cloudSprite = new Sprite2D
            {
                Texture = cloudTex,
                Position = new Vector2(cx, cy),
                Scale = Vector2.One * scale,
                Modulate = new Color(1, 1, 1, Math.Clamp(scale * 0.35f, 0.35f, 0.85f)),
                ZIndex = scale > 1.8f ? 3 : -1
            };
            moving.AddChild(cloudSprite);
            flightClouds.Add((cloudSprite, new Vector2(cx, cy), cloudRng.RandfRange(-20, -5) * scale, scale));
        }

        obstacles = new CoilStoryObstacles(physics, moving, PortProfile.CurrentSlot.StoryModeSpaceLevel);
        penguinPivot = new Node2D();
        moving.AddChild(penguinPivot);
        Sprite(penguinPivot, Asset("Penguin"), new(-76.65f, -137.35f));
        coil = Sprite(moving, CoilFrame(1), new Vector2(-133.85f, -244.9f) * .4f + new Vector2(0, 1000));
        coil.Scale = new(.4f, .4f);

        // Wind / speed streaks (DragWind.as)
        windParticles = new CpuParticles2D
        {
            Position = new Vector2(480, -20),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(480, 5),
            Direction = Vector2.Down,
            Spread = 2,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 800,
            InitialVelocityMax = 1600,
            ScaleAmountMin = 1.0f,
            ScaleAmountMax = 2.0f,
            Color = new Color(1, 1, 1, 0.35f),
            Amount = 30,
            Lifetime = 0.4,
            Emitting = false
        };
        AddChild(windParticles);

        hud = new Node2D { Position = new Vector2(0, 1000) };
        AddChild(hud);
        Sprite(hud, Asset("Speed"), new(812 - 68.7f, 474 - 68.8f));
        var needlePivot = new Node2D { Position = new(811.75f, 473.2f), RotationDegrees = -135, Scale = new(.3f, .3f) };
        hud.AddChild(needlePivot);
        speedNeedle = Sprite(needlePivot, Asset("SpeedNeedle"), new(-17.5f, -177.2f));
        dragGauge = Sprite(hud, Asset("Drag"), new(935 - 56.55f, 333 - 46.1f));
        Sprite(hud, Asset("Altitude"), new(919 - 82.4f, 456 - 136.75f));

        // Dynamic rolling altitude tape (AltitudeNeedleHud.as)
        var altWindow = new Control
        {
            Position = new Vector2(887.65f, 380.45f),
            Size = new Vector2(62.75f, 151.05f),
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        hud.AddChild(altWindow);

        var altBg = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(62.75f, 151.05f),
            Color = new Color(0.92f, 0.92f, 0.92f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        altWindow.AddChild(altBg);

        altTapeNode = new Node2D { Position = new Vector2(0, 75.5f) };
        altWindow.AddChild(altTapeNode);

        float[] majorTicks = { -126f, -63f, 0f, 63f, 126f };
        foreach (float ty in majorTicks)
        {
            var tick = new ColorRect
            {
                Position = new Vector2(52f, ty - 0.5f),
                Size = new Vector2(10.75f, 1.5f),
                Color = Colors.Black,
                MouseFilter = MouseFilterEnum.Ignore
            };
            altTapeNode.AddChild(tick);
        }
        float[] minorTicks = { -94.5f, -31.5f, 31.5f, 94.5f };
        foreach (float ty in minorTicks)
        {
            var tick = new ColorRect
            {
                Position = new Vector2(48f, ty - 0.5f),
                Size = new Vector2(14.75f, 1.5f),
                Color = Colors.Black,
                MouseFilter = MouseFilterEnum.Ignore
            };
            altTapeNode.AddChild(tick);
        }

        var font = GD.Load<Font>("res://Assets/Start/font.ttf");
        for (int i = 0; i < 5; i++)
        {
            float ty = majorTicks[i];
            var lbl = new Label
            {
                Position = new Vector2(0, ty - 12),
                Size = new Vector2(46, 24),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            lbl.AddThemeFontOverride("font", font);
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.AddThemeColorOverride("font_color", new Color(0.12f, 0.12f, 0.12f));
            altTapeNode.AddChild(lbl);
            altLabels[i] = lbl;
        }

        var altNeedleArrow = new Polygon2D
        {
            Polygon = new Vector2[] { new(0, 63.95f), new(11.55f, 75.5f), new(0, 87.05f) },
            Color = new Color(0.85f, 0.15f, 0.1f)
        };
        altWindow.AddChild(altNeedleArrow);

        timerPanel = new Node2D { Position = new(0, -100), Visible = false };
        AddChild(timerPanel);
        timeLabel = new Label { Position = new(727.35f, 8.2f), Size = new(134, 28), MouseFilter = MouseFilterEnum.Ignore };
        timeLabel.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Start/numbers.ttf"));
        timeLabel.AddThemeFontSizeOverride("font_size", 24);
        timeLabel.AddThemeConstantOverride("outline_size", 5);
        timeLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        timeLabel.AddThemeColorOverride("font_color", Colors.White);
        timerPanel.AddChild(timeLabel);
        abort = Sprite(timerPanel, Asset("Abort"), AbortOrigin);

        abortWarning = Sprite(this, Asset("AbortWarning"), new(480 - 76f, 220f));
        abortTimer = Sprite(this, Asset("AbortTimer"), new(480 - 52f, 260f));
        abortTimer.Scale = new(2.34448f, 2.34448f);
        abortWarning.Hide();
        abortTimer.Hide();
        flightMusic = OriginalMusic.CreateFlightSet(this);
        DrawWorld();
    }

    private static Sprite2D Sprite(Node parent, Texture2D texture, Vector2 position)
    {
        var result = new Sprite2D { Texture = texture, Position = position, Centered = false };
        parent.AddChild(result);
        return result;
    }

    private static Texture2D Asset(string folder, int frame = 1) => GD.Load<Texture2D>($"{Art}{folder}/{frame}.png");
    private static Texture2D SkyFrame(int frame) => GD.Load<Texture2D>($"res://Assets/Flight/Sky/{frame}.png");
    private Texture2D CoilFrame(int frame) => coilFrames[frame] ??= GD.Load<Texture2D>($"res://Assets/Flight/Coil/{frame}.png");

    public override void _Input(InputEvent input)
    {
        if (speaker == null || TransitionOverlay.IsActive(GetTree())) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
        {
            if (!click.Pressed && optionsButton != null && new Rect2(optionsButton.Position, optionsButton.Texture.GetSize() * optionsButton.Scale).HasPoint(GetLocalMousePosition()))
            {
                GetViewport().SetInputAsHandled();
                return;
            }
            if (!click.Pressed && new Rect2(speaker.Position, speaker.Texture.GetSize() * speaker.Scale).HasPoint(GetLocalMousePosition()))
            {
                OriginalAudio.ToggleMute();
                speaker.Texture = OriginalAudio.SpeakerTexture();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (!Progress && !ending)
            {
                if (!click.Pressed) mouseAbort = false;
                else if (sinceTakeoff >= 50 && AbortHit()) mouseAbort = true;
            }
        }
        if (Progress && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter, AltPressed: false })
            Takeoff();
    }

    public override void _Process(double delta)
    {
        GuiTick.Pump(delta);
        if (verifyCapture)
        {
            captureTicks++;
            if (captureTicks == 15)
            {
                GetViewport().GetTexture().GetImage().SavePng("res://coil-flight-launchpad.png");
            }
            else if (captureTicks == 130)
            {
                GetViewport().GetTexture().GetImage().SavePng("res://coil-flight-airborne.png");
            }
            else if (captureTicks == 180 && !ending)
            {
                FinishFlight();
            }
            else if (captureTicks == 340)
            {
                GetViewport().GetTexture().GetImage().SavePng("res://coil-flight-results.png");
                GD.Print("COIL_FLIGHT_CAPTURE_PASS: launchpad, airborne, results captured.");
                GetTree().Quit();
                return;
            }
        }
        if (Progress || ending || physics == null) return;
        accumulator += Math.Min(delta, .25);
        while (accumulator >= 1d / CoilStoryPhysics.FrameRate && !ending)
        {
            accumulator -= 1d / CoilStoryPhysics.FrameRate;
            TickFlight();
        }
    }

    private void TickFlight()
    {
        int steering = Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A) ? -1
            : Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D) ? 1 : 0;
        physics.Step(steering, obstacles.ReadContacts);
        if (coilFrame < FinalCoilFrame)
        {
            coil.Texture = CoilFrame(++coilFrame);
            if (!physics.Launched)
            {
                var t = tracer[coilFrame - 1];
                physics.MoveOnTracer(t[0], 1000 + t[1], t[2]);
            }
            if (coilFrame == ReleaseFrame)
            {
                physics.Takeoff();
                OriginalAudio.PlaySfx(this, OriginalSound.SpringBoing);
                flightMusic.Play();
                timerPanel.Show();
            }
        }
        if (physics.Launched)
        {
            sinceTakeoff += CoilStoryPhysics.Tick;
            flightMusic.UpdateSpeed(physics.Speed);
            obstacles.Advance(zoom);
            UpdateClouds((float)CoilStoryPhysics.Tick);
            UpdateHud();
        }
        DrawWorld();
        if (physics.DayEndTicks >= 150 || abortHeld >= 60) FinishFlight();
    }

    private void UpdateClouds(float dt)
    {
        float px = (float)physics.X;
        float py = (float)physics.Y;
        for (int i = 0; i < flightClouds.Count; i++)
        {
            var (sprite, pos, speed, scale) = flightClouds[i];
            pos.X += speed * 0.1f * dt;
            if (pos.Y > py + 700)
            {
                pos.Y = py - 700 - (float)GD.RandRange(0, 350);
                pos.X = px + (float)GD.RandRange(-1200, 1200);
            }
            else if (pos.Y < py - 1200)
            {
                pos.Y = py + 700 + (float)GD.RandRange(0, 350);
                pos.X = px + (float)GD.RandRange(-1200, 1200);
            }
            if (pos.X < px - 1600) pos.X += 3200;
            else if (pos.X > px + 1600) pos.X -= 3200;

            sprite.Position = pos;
            flightClouds[i] = (sprite, pos, speed, scale);

            double spaceGoal = PortProfile.CurrentSlot.StoryModeSpaceLevel;
            if (spaceGoal > 0 && physics.Altitude > spaceGoal * 0.6)
            {
                float fade = (float)Math.Clamp(1.0 - (physics.Altitude - spaceGoal * 0.6) / (spaceGoal * 0.3), 0, 1);
                sprite.Modulate = new Color(1, 1, 1, Math.Clamp(scale * 0.35f, 0.35f, 0.85f) * fade);
            }
        }
    }

    private void DrawWorld()
    {
        var location = new Vector2((float)physics.X, (float)physics.Y);
        penguinPivot.Position = location;
        penguinPivot.Rotation = (float)physics.Rotation;
        int newSky = (int)CoilStoryPhysics.Curve(1, MaxSkyFrame, 0, PortProfile.CurrentSlot.StoryModeSpaceLevel, physics.Altitude);
        if (newSky != skyFrame) sky.Texture = SkyFrame(skyFrame = newSky);
        double baseZoom = CoilStoryPhysics.Curve(1, 2.5, 0, 30, physics.Speed);
        float targetZoom = (float)(1 / CoilStoryPhysics.Curve(1, 4, 1, 3.5, baseZoom, .7));
        zoom += (targetZoom - zoom) * .01f;
        moving.Scale = new Vector2(zoom, zoom);
        double lead = CoilStoryPhysics.Curve(0, -250, 0, 60, physics.Speed, .7);
        cameraOffsets.Dequeue();
        cameraOffsets.Enqueue(CoilStoryPhysics.Curve(80, 0, 0, 80, Math.Abs(lead), .6));
        
        Vector2 targetLook = Vector2.Zero;
        if (physics.Launched && physics.Speed > 0.5)
        {
            targetLook = Vector2.FromAngle((float)physics.SpeedAngle) * (float)lead;
        }

        // Match Flash GameCamera.as LEAD_SMOOTHING = 0.05 per frame
        smoothedLook = smoothedLook.Lerp(targetLook, 0.05f);
        Vector2 targetCamPos = ViewSize / 2 + smoothedLook + new Vector2(0, (float)cameraOffsets.Average()) - location * zoom;
        if (!camInitialized)
        {
            smoothedCamPos = targetCamPos;
            camInitialized = true;
        }
        else
        {
            // Smooth camera position to absorb high-frequency collision/jitter
            smoothedCamPos = smoothedCamPos.Lerp(targetCamPos, 0.25f);
        }
        moving.Position = smoothedCamPos;
        float center = (float)Math.Floor((physics.X + 1000) / 2000) * GroundTileWidth;
        for (int i = 0; i < groundTiles.Count; i++)
            groundTiles[i].Position = new(center + (i - 2) * GroundTileWidth - 1000.5f, 668.7f);

        windParticles.Emitting = physics.Launched && physics.Speed > 10;
        if (windParticles.Emitting)
        {
            float v = (float)physics.Speed * 30f;
            windParticles.InitialVelocityMin = Math.Max(400, v * 0.8f);
            windParticles.InitialVelocityMax = Math.Max(800, v * 1.2f);
        }
    }

    private bool AbortHit() => new Rect2(867, 0, 93, 41).HasPoint(GetLocalMousePosition());

    private void UpdateHud()
    {
        double timerReveal = CoilStoryPhysics.Curve(0, 1, 0, 50, sinceTakeoff);
        timerPanel.Position = new(0, (float)(-100 * Math.Pow(1 - timerReveal, 3)));
        hud.Position = new(0, (float)(1000 * Math.Pow(1 - CoilStoryPhysics.Curve(0, 1, 0, 70, sinceTakeoff), 5)));
        double seconds = physics.FlightTimeMs / 1000;
        double remainder = seconds % 60;
        string prefix = remainder < 10 ? "0" : "";
        timeLabel.Text = FormattableString.Invariant($"{(int)(seconds / 60)}:{prefix}{remainder:0.0}");
        timeLabel.AddThemeColorOverride("font_color", physics.DayEndTicks >= 1 ? new Color(0.996f, 0.698f, 0.333f) : Colors.White);
        var needle = (Node2D)speedNeedle.GetParent();
        double targetAngle = Math.Clamp(-135 + 180 * physics.Speed / physics.TopSpeed, -135, 135);
        needle.RotationDegrees += (float)((targetAngle - needle.RotationDegrees) * .05);
        int nextDrag = (int)CoilStoryPhysics.Curve(1, 500, 1, .92, physics.CurrentDrag);
        if (nextDrag != dragFrame) dragGauge.Texture = Asset("Drag", dragFrame = nextDrag);

        double alt = physics.Altitude;
        int centerK = (int)Math.Floor(alt / 1000.0);
        double altRemainder = alt - centerK * 1000.0;
        altTapeNode.Position = new Vector2(0, (float)(75.5f + altRemainder / 1000.0 * 63.0));

        for (int i = 0; i < 5; i++)
        {
            int val = centerK + 2 - i;
            if (val > 0)
                altLabels[i].Text = $"{val}k";
            else if (val == 0)
                altLabels[i].Text = "0";
            else
                altLabels[i].Text = "";
        }

        bool aborting = sinceTakeoff >= 50 && (mouseAbort || Input.IsKeyPressed(Key.Enter));
        abortHeld = aborting ? abortHeld + CoilStoryPhysics.Tick : 0;
        abort.Texture = Asset("Abort", aborting ? 3 : AbortHit() ? 2 : 1);
        abortWarning.Visible = abortTimer.Visible = aborting;
        if (aborting)
            abortTimer.Texture = Asset("AbortTimer", (int)CoilStoryPhysics.Curve(1, 200, 0, 60, abortHeld));
        if (physics.DayEndTicks >= 1)
            flightMusic.SetDayEndGain((float)Math.Clamp(1 - physics.DayEndTicks / 150 * .3, .7, 1));
    }

    private void FinishFlight()
    {
        ending = true;
        flightMusic.Stop();
        abortWarning.Hide();
        abortTimer.Hide();
        var stats = new CoilDayStats(physics.MaxAltitude, physics.FlightTimeMs, physics.MaxSpeed,
            physics.MaxDistanceLeft, physics.MaxDistanceRight, obstacles.HitCount, MoneyMultiplier());
        CoilStoryResults.Show(this, stats, reward => PortProfile.CompleteCoilDay(slotIndex, day, stats, reward), ReturnToShop);
    }

    private static double MoneyMultiplier()
    {
        var entries = OriginalCatalog.Load().ToDictionary(e => e.id);
        return entries["bonusMoney"].Value("statPower", PortProfile.State.BonusLevels.GetValueOrDefault("bonusMoney"))
            * entries["medalCash"].Value("statPower", PortProfile.State.Medals.GetValueOrDefault("medalCash"))
            * entries["medalFrequentBuyer"].Value("statPower", PortProfile.State.Medals.GetValueOrDefault("medalFrequentBuyer"));
    }

    public override void _ExitTree()
    {
        foreach (var bounce in progressButtons) bounce.Destroy();
    }
}
