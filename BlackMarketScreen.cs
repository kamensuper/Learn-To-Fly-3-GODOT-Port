using Godot;
using System;
using System.Collections.Generic;

// Source-faithful port of src.paidshop.PaidShop and Merchant / MerchantBubble.
// Viewport: 960 x 540 native coordinate system.
// Music: 77_MusicBonusShop3.ogg (MusicBonusShop3) looped on entrance.
public partial class BlackMarketScreen : Control
{
    private const float DesignWidth = 960f;
    private const float DesignHeight = 540f;

    // Shelf scaling and scrolling (PaidShop.as:268-272)
    private const float ShelfScale = 540f / 1149f; // ~0.47
    private const float CounterScale = 540f / 1149f;
    private const float MerchantScale = 540f / 1149f;

    private Control _world = null!;
    private Control _shelfContainer = null!;
    private TextureRect _shelfArt = null!;
    private TextureRect _counterArt = null!;
    private Control _merchantContainer = null!;
    private TextureRect _merchantArt = null!;
    private TextureRect _bubbleArt = null!;
    private Label _sardineCountLabel = null!;
    private Label _earninatorCount = null!;
    private Label _floatifierCount = null!;
    private Label _speedifierCount = null!;

    // Dialog elements
    private Control _dialogOverlay = null!;
    private Panel _dialogBox = null!;
    private Label _dialogTitle = null!;
    private Label _dialogDesc = null!;
    private Label _dialogCost = null!;
    private Button _dialogOkBtn = null!;
    private Button _dialogCancelBtn = null!;
    private Action? _dialogOnOk;

    private readonly List<Texture2D> _bubbleFrames = new();
    private AudioStreamPlayer _music = null!;

    // Easing state
    private float _scrollTargetX = 60f;
    private float _scrollCurrentX = 60f;

    // Merchant ducking state (Merchant.as:36-61)
    private float _merchantCurrentX = 120f;
    private float _merchantStartX = 120f;
    private float _merchantTargetX = 120f;
    private double _merchantTime = 100;
    private const double MerchantDuration = 100; // 100 ticks / frames in AS3
    private const float PosLeft = -360f;
    private const float PosRight = 120f;
    private const float MouseCutoff = 720f;

    // Bubble state
    private double _bubbleTimer;
    private bool _bubbleHoverActive;
    private int _currentBubbleFrame = 1;
    private double _captureElapsed;

    // Interactive item definitions on the shelf
    private sealed class ShelfItem
    {
        public string Id = "";
        public string Title = "";
        public string Description = "";
        public int Cost;
        public int BubbleFrame;
        public Rect2 Bounds; // in shelf local coordinates (at ShelfScale)
    }

    private readonly List<ShelfItem> _items = new();

    public override void _Ready()
    {
        OriginalAudio.Ensure();
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Music: 77_MusicBonusShop3.ogg
        _music = OriginalMusic.Create(this);
        OriginalMusic.Play(_music, OriginalMusic.BlackMarket);

        var viewport = GetViewport().GetVisibleRect().Size;
        float s = Math.Min(viewport.X / DesignWidth, viewport.Y / DesignHeight);
        _world = new Control
        {
            Scale = new Vector2(s, s),
            Position = (viewport - new Vector2(DesignWidth, DesignHeight) * s) * 0.5f,
            Size = new Vector2(DesignWidth, DesignHeight),
            ClipContents = true
        };
        AddChild(_world);

        // Dark atmospheric room background
        var bg = new ColorRect
        {
            Color = new Color(0.06f, 0.05f, 0.05f, 1f),
            Size = new Vector2(DesignWidth, DesignHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _world.AddChild(bg);

        // Shelf Container (moves horizontally with mouse)
        _shelfContainer = new Control
        {
            Position = new Vector2(60, 0),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _world.AddChild(_shelfContainer);

        _shelfArt = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/BlackMarket/shelf.png"),
            Scale = new Vector2(ShelfScale, ShelfScale),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _shelfContainer.AddChild(_shelfArt);

        // Setup shelf item regions
        SetupShelfItems();

        // Merchant container (behind counter, in front of shelf)
        _merchantContainer = new Control
        {
            Position = new Vector2(PosRight, 200),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _world.AddChild(_merchantContainer);

        _merchantArt = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/BlackMarket/merchant.png"),
            Scale = Vector2.One,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _merchantContainer.AddChild(_merchantArt);

        // Merchant speech bubble
        for (int i = 1; i <= 22; i++)
        {
            _bubbleFrames.Add(GD.Load<Texture2D>($"res://Assets/BlackMarket/bubble/{i}.png"));
        }

        _bubbleArt = new TextureRect
        {
            Texture = _bubbleFrames[0],
            Position = new Vector2(170, -110),
            Scale = new Vector2(0.95f, 0.95f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _merchantContainer.AddChild(_bubbleArt);

        // Counter (foreground desk)
        _counterArt = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/BlackMarket/counter.png"),
            Position = new Vector2(0, 375),
            Scale = new Vector2(CounterScale * 1.52f, CounterScale * 1.52f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _world.AddChild(_counterArt);

        // HUD Elements
        CreateHud();

        // Modal Dialog for Purchases
        CreateDialog();

        // Initial refresh
        RefreshUi();
    }

    private void SetupShelfItems()
    {
        // Coordinates calibrated to shelf.png scaled by ShelfScale
        _items.Add(new ShelfItem
        {
            Id = "arcade",
            Title = "Dodo Invaders Arcade",
            Description = "An authentic retro mini-game cabinet!",
            Cost = 0,
            BubbleFrame = 1,
            Bounds = new Rect2(20, 40, 160, 460)
        });

        _items.Add(new ShelfItem
        {
            Id = "items",
            Title = "Items Pack",
            Description = "Unlock all story and classic flight items immediately!",
            Cost = 175,
            BubbleFrame = 15,
            Bounds = new Rect2(200, 130, 125, 140)
        });

        _items.Add(new ShelfItem
        {
            Id = "upgrades",
            Title = "Upgrades Pack",
            Description = "Max out all flight upgrades instantly!",
            Cost = 175,
            BubbleFrame = 16,
            Bounds = new Rect2(335, 130, 125, 140)
        });

        _items.Add(new ShelfItem
        {
            Id = "cheats",
            Title = "Cheats Pack",
            Description = "Unlock the secret game modifiers & physics cheats!",
            Cost = 100,
            BubbleFrame = 17,
            Bounds = new Rect2(470, 130, 125, 140)
        });

        _items.Add(new ShelfItem
        {
            Id = "time",
            Title = "Time Warp",
            Description = "Fast-forward simulation time for rapid testing!",
            Cost = 100,
            BubbleFrame = 18,
            Bounds = new Rect2(605, 130, 125, 140)
        });

        _items.Add(new ShelfItem
        {
            Id = "bundle",
            Title = "Everything Bundle",
            Description = "Unlock Items, Upgrades, Cheats, and Time Warp in one pack!",
            Cost = 450,
            BubbleFrame = 14,
            Bounds = new Rect2(200, 275, 530, 160)
        });

        _items.Add(new ShelfItem
        {
            Id = "earninator",
            Title = "Earninator Consumable",
            Description = "Doubles all flight earnings during your launch!",
            Cost = 10,
            BubbleFrame = 19,
            Bounds = new Rect2(745, 130, 100, 130)
        });

        _items.Add(new ShelfItem
        {
            Id = "floatifier",
            Title = "Floatifier Consumable",
            Description = "Grants lighter gravity and prolonged airtime!",
            Cost = 10,
            BubbleFrame = 20,
            Bounds = new Rect2(855, 130, 100, 130)
        });

        _items.Add(new ShelfItem
        {
            Id = "speedifier",
            Title = "Speedifier Consumable",
            Description = "Boosts your vehicle to extreme velocity!",
            Cost = 10,
            BubbleFrame = 21,
            Bounds = new Rect2(965, 130, 100, 130)
        });

        _items.Add(new ShelfItem
        {
            Id = "booster",
            Title = "Booster Pack",
            Description = "Contains random cards and permanent bonuses!",
            Cost = 30,
            BubbleFrame = 22,
            Bounds = new Rect2(1075, 130, 120, 260)
        });
    }

    private void CreateHud()
    {
        var font = GD.Load<FontFile>("res://Assets/Start/numbers.ttf");

        // Top-Right Sardines Counter (fp bar)
        var sardineBox = new HBoxContainer { Position = new Vector2(790, 15), MouseFilter = MouseFilterEnum.Ignore };
        var fishIcon = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/Start/sardines.png"),
            CustomMinimumSize = new Vector2(30, 30),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _sardineCountLabel = new Label
        {
            Text = PortProfile.State.Sardines.ToString("N0"),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _sardineCountLabel.AddThemeFontOverride("font", font);
        _sardineCountLabel.AddThemeFontSizeOverride("font_size", 22);
        _sardineCountLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _sardineCountLabel.AddThemeConstantOverride("outline_size", 5);

        sardineBox.AddChild(fishIcon);
        sardineBox.AddChild(_sardineCountLabel);
        _world.AddChild(sardineBox);

        // Top-Center Consumable Counters
        var powerBox = new HBoxContainer { Position = new Vector2(320, 15), MouseFilter = MouseFilterEnum.Ignore };
        powerBox.AddThemeConstantOverride("separation", 24);

        _earninatorCount = CreatePowerLabel(powerBox, "Earn: ", font);
        _floatifierCount = CreatePowerLabel(powerBox, "Float: ", font);
        _speedifierCount = CreatePowerLabel(powerBox, "Speed: ", font);
        _world.AddChild(powerBox);

        // Bottom-Left "DONE" Button
        var doneBtn = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/BlackMarket/btn_done.png"),
            Position = new Vector2(30, 480),
            Scale = Vector2.One * 1.05f
        };
        var doneBounce = new MouseBounce2(doneBtn) { ScaleMod = 0.08 };
        doneBounce.SignalClick += _ => OnDoneClicked();
        _world.AddChild(doneBtn);

        // Bottom-Right "MY PURCHASES" Button
        var purchasesBtn = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/BlackMarket/btn_purchases.png"),
            Position = new Vector2(740, 480),
            Scale = Vector2.One * 0.95f
        };
        var purchasesBounce = new MouseBounce2(purchasesBtn) { ScaleMod = 0.08 };
        purchasesBounce.SignalClick += _ =>
        {
            OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);
            ShowDialog("My Purchases",
                $"Earninator: {PortProfile.State.ConsumableEarninator}\n" +
                $"Floatifier: {PortProfile.State.ConsumableFloatifier}\n" +
                $"Speedifier: {PortProfile.State.ConsumableSpeedifier}\n" +
                $"Items Pack: {(PortProfile.State.BoughtItems ? "PURCHASED" : "Not Owned")}\n" +
                $"Upgrades Pack: {(PortProfile.State.BoughtUpgrades ? "PURCHASED" : "Not Owned")}\n" +
                $"Cheats Pack: {(PortProfile.State.BoughtCheats ? "PURCHASED" : "Not Owned")}\n" +
                $"Time Warp: {(PortProfile.State.BoughtTimeWarp ? "PURCHASED" : "Not Owned")}\n" +
                $"Everything Bundle: {(PortProfile.State.BoughtBundle ? "PURCHASED" : "Not Owned")}",
                cost: 0, onOk: null);
        };
        _world.AddChild(purchasesBtn);
    }

    private Label CreatePowerLabel(HBoxContainer parent, string prefix, FontFile font)
    {
        var lbl = new Label { Text = prefix + "0", MouseFilter = MouseFilterEnum.Ignore };
        lbl.AddThemeFontOverride("font", font);
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
        lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
        lbl.AddThemeConstantOverride("outline_size", 4);
        parent.AddChild(lbl);
        return lbl;
    }

    private void CreateDialog()
    {
        var font = GD.Load<FontFile>("res://Assets/Start/numbers.ttf");

        _dialogOverlay = new Control
        {
            Size = new Vector2(DesignWidth, DesignHeight),
            Visible = false,
            ZIndex = 100
        };
        var modalDim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.65f),
            Size = new Vector2(DesignWidth, DesignHeight)
        };
        _dialogOverlay.AddChild(modalDim);

        _dialogBox = new Panel
        {
            Position = new Vector2(280, 150),
            Size = new Vector2(400, 240)
        };
        var boxStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.14f, 0.95f),
            BorderColor = new Color(0.85f, 0.7f, 0.25f, 1f),
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        _dialogBox.AddThemeStyleboxOverride("panel", boxStyle);
        _dialogOverlay.AddChild(_dialogBox);

        _dialogTitle = new Label
        {
            Position = new Vector2(20, 18),
            Size = new Vector2(360, 30),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _dialogTitle.AddThemeFontOverride("font", font);
        _dialogTitle.AddThemeFontSizeOverride("font_size", 20);
        _dialogTitle.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        _dialogBox.AddChild(_dialogTitle);

        _dialogDesc = new Label
        {
            Position = new Vector2(25, 60),
            Size = new Vector2(350, 80),
            AutowrapMode = TextServer.AutowrapMode.Word,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _dialogDesc.AddThemeFontSizeOverride("font_size", 14);
        _dialogBox.AddChild(_dialogDesc);

        _dialogCost = new Label
        {
            Position = new Vector2(20, 145),
            Size = new Vector2(360, 25),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _dialogCost.AddThemeFontOverride("font", font);
        _dialogCost.AddThemeFontSizeOverride("font_size", 18);
        _dialogCost.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        _dialogBox.AddChild(_dialogCost);

        _dialogOkBtn = new Button
        {
            Text = "BUY",
            Position = new Vector2(60, 185),
            Size = new Vector2(120, 35)
        };
        _dialogOkBtn.Pressed += () =>
        {
            OriginalAudio.PlaySfx(this, OriginalSound.Cash);
            _dialogOnOk?.Invoke();
            _dialogOverlay.Visible = false;
            RefreshUi();
        };
        _dialogBox.AddChild(_dialogOkBtn);

        _dialogCancelBtn = new Button
        {
            Text = "CANCEL",
            Position = new Vector2(220, 185),
            Size = new Vector2(120, 35)
        };
        _dialogCancelBtn.Pressed += () =>
        {
            OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);
            _dialogOverlay.Visible = false;
        };
        _dialogBox.AddChild(_dialogCancelBtn);

        _world.AddChild(_dialogOverlay);
    }

    private void ShowDialog(string title, string desc, int cost, Action? onOk)
    {
        _dialogTitle.Text = title;
        _dialogDesc.Text = desc;
        _dialogCost.Text = cost > 0 ? $"Cost: {cost:N0} Sardines" : "";
        _dialogOkBtn.Text = cost > 0 ? "BUY" : "OK";
        _dialogCancelBtn.Visible = cost > 0;
        if (cost <= 0) _dialogOkBtn.Position = new Vector2(140, 185);
        else _dialogOkBtn.Position = new Vector2(60, 185);

        bool canAfford = cost <= 0 || PortProfile.State.Sardines >= cost;
        _dialogOkBtn.Disabled = !canAfford;
        if (!canAfford)
        {
            _dialogCost.Text += " (NOT ENOUGH SARDINES)";
            _dialogCost.AddThemeColorOverride("font_color", Colors.Crimson);
        }
        else
        {
            _dialogCost.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        }

        _dialogOnOk = () =>
        {
            if (cost > 0 && PortProfile.State.Sardines >= cost)
            {
                PortProfile.State.Sardines -= cost;
                onOk?.Invoke();
                PortProfile.Save();
            }
        };

        _dialogOverlay.Visible = true;
    }

    private void OnDoneClicked()
    {
        OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);
        if (PortProfile.BlackMarketReturnToEquipmentShop)
        {
            PortProfile.BlackMarketReturnToEquipmentShop = false;
            PortProfile.ResumeEquipmentShop = true;
        }
        TransitionOverlay.To(this, "res://StartScreen.tscn");
    }

    public override void _Process(double delta)
    {
        GuiTick.Pump(delta);

        if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-black-market"))
        {
            _merchantCurrentX = 200f;
            _merchantTargetX = 200f;
            _captureElapsed += delta;
            if (_captureElapsed >= 0.5)
            {
                GetViewport().GetTexture().GetImage().SavePng("res://black-market-preview.png");
                GetTree().Quit();
            }
        }

        Vector2 mousePos = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-black-market")
            ? new Vector2(350, 250)
            : _world.GetLocalMousePosition();

        // 1. Horizontal shelf scroll easing (PaidShop.as:268-272)
        // targetX = Easing.easeBothInOutQuad(mouseX, 60, -250, 960)
        _scrollTargetX = (float)Easing.EaseBothInOutQuad(Math.Clamp(mousePos.X, 0, DesignWidth), 60, -250, DesignWidth);
        _scrollCurrentX += (_scrollTargetX - _scrollCurrentX) * 0.05f;
        _shelfContainer.Position = new Vector2(_scrollCurrentX, 0);

        // 2. Merchant position easing (Merchant.as:36-61)
        if (_merchantTargetX != PosLeft && mousePos.X >= MouseCutoff)
        {
            _merchantTime = 0;
            _merchantTargetX = PosLeft;
            _merchantStartX = _merchantCurrentX;
        }
        else if (_merchantTargetX != PosRight && mousePos.X < MouseCutoff)
        {
            _merchantTime = 0;
            _merchantTargetX = PosRight;
            _merchantStartX = _merchantCurrentX;
        }

        if (_merchantTime < MerchantDuration)
        {
            _merchantTime += delta * 60;
            _merchantCurrentX = (float)Easing.EaseOutQuint(
                Math.Min(_merchantTime, MerchantDuration),
                _merchantStartX,
                _merchantTargetX - _merchantStartX,
                MerchantDuration);
        }
        else
        {
            _merchantCurrentX = _merchantTargetX;
        }
        _merchantContainer.Position = new Vector2(_merchantCurrentX, 200);

        // 3. Hover detection over shelf items
        Vector2 shelfMouse = mousePos - _shelfContainer.Position;
        ShelfItem? hovered = null;
        if (!_dialogOverlay.Visible)
        {
            foreach (var item in _items)
            {
                if (item.Bounds.HasPoint(shelfMouse))
                {
                    hovered = item;
                    break;
                }
            }
        }

        if (hovered != null)
        {
            _bubbleHoverActive = true;
            SetBubbleFrame(hovered.BubbleFrame);
        }
        else
        {
            if (_bubbleHoverActive)
            {
                _bubbleHoverActive = false;
                _bubbleTimer = 0;
                SetBubbleFrame(1);
            }

            // Idle quip cycling every 3.5 seconds
            _bubbleTimer += delta;
            if (_bubbleTimer >= 3.5)
            {
                _bubbleTimer = 0;
                int randomQuip = (int)(GD.Randi() % 13) + 1;
                SetBubbleFrame(randomQuip);
            }
        }
    }

    private void SetBubbleFrame(int frame)
    {
        int index = Math.Clamp(frame - 1, 0, _bubbleFrames.Count - 1);
        if (_currentBubbleFrame != frame)
        {
            _currentBubbleFrame = frame;
            _bubbleArt.Texture = _bubbleFrames[index];
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_dialogOverlay.Visible || TransitionOverlay.IsActive(GetTree())) return;

        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
        {
            Vector2 mousePos = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-black-market")
            ? new Vector2(350, 250)
            : _world.GetLocalMousePosition();
            Vector2 shelfMouse = mousePos - _shelfContainer.Position;

            foreach (var item in _items)
            {
                if (item.Bounds.HasPoint(shelfMouse))
                {
                    OnItemClicked(item);
                    GetViewport().SetInputAsHandled();
                    break;
                }
            }
        }
    }

    private void OnItemClicked(ShelfItem item)
    {
        OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);

        switch (item.Id)
        {
            case "arcade":
                ShowDialog("Dodo Invaders", "You found the secret Dodo Invaders arcade machine! Start mini-game?", 0, () =>
                {
                    OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);
                });
                break;

            case "items":
                if (PortProfile.State.BoughtItems)
                {
                    ShowDialog("Items Pack", "Already purchased! All flight items are permanently unlocked.", 0, null);
                }
                else
                {
                    ShowDialog(item.Title, item.Description, item.Cost, () =>
                    {
                        PortProfile.State.BoughtItems = true;
                    });
                }
                break;

            case "upgrades":
                if (PortProfile.State.BoughtUpgrades)
                {
                    ShowDialog("Upgrades Pack", "Already purchased! All upgrades are permanently maxed out.", 0, null);
                }
                else
                {
                    ShowDialog(item.Title, item.Description, item.Cost, () =>
                    {
                        PortProfile.State.BoughtUpgrades = true;
                    });
                }
                break;

            case "cheats":
                if (PortProfile.State.BoughtCheats)
                {
                    ShowDialog("Cheats Pack", "Already purchased! Cheats menu is unlocked.", 0, null);
                }
                else
                {
                    ShowDialog(item.Title, item.Description, item.Cost, () =>
                    {
                        PortProfile.State.BoughtCheats = true;
                    });
                }
                break;

            case "time":
                if (PortProfile.State.BoughtTimeWarp)
                {
                    ShowDialog("Time Warp", "Already purchased! Fast-forward speeds are unlocked.", 0, null);
                }
                else
                {
                    ShowDialog(item.Title, item.Description, item.Cost, () =>
                    {
                        PortProfile.State.BoughtTimeWarp = true;
                    });
                }
                break;

            case "bundle":
                if (PortProfile.State.BoughtBundle)
                {
                    ShowDialog("Everything Bundle", "Already purchased! You own every Black Market pack.", 0, null);
                }
                else
                {
                    ShowDialog(item.Title, item.Description, item.Cost, () =>
                    {
                        PortProfile.State.BoughtBundle = true;
                        PortProfile.State.BoughtItems = true;
                        PortProfile.State.BoughtUpgrades = true;
                        PortProfile.State.BoughtCheats = true;
                        PortProfile.State.BoughtTimeWarp = true;
                    });
                }
                break;

            case "earninator":
                ShowDialog(item.Title, item.Description, item.Cost, () =>
                {
                    PortProfile.State.ConsumableEarninator++;
                });
                break;

            case "floatifier":
                ShowDialog(item.Title, item.Description, item.Cost, () =>
                {
                    PortProfile.State.ConsumableFloatifier++;
                });
                break;

            case "speedifier":
                ShowDialog(item.Title, item.Description, item.Cost, () =>
                {
                    PortProfile.State.ConsumableSpeedifier++;
                });
                break;

            case "booster":
                ShowDialog(item.Title, item.Description, item.Cost, () =>
                {
                    PortProfile.State.BonusPoints += 150;
                });
                break;
        }
    }

    private void RefreshUi()
    {
        _sardineCountLabel.Text = PortProfile.State.Sardines.ToString("N0");
        _earninatorCount.Text = $"Earn: {PortProfile.State.ConsumableEarninator}";
        _floatifierCount.Text = $"Float: {PortProfile.State.ConsumableFloatifier}";
        _speedifierCount.Text = $"Speed: {PortProfile.State.ConsumableSpeedifier}";
    }
}
