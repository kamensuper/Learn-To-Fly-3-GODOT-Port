using Godot;
using System;

// Translation of src.gui.Transition.
//
// Transition.as drives a MovieClip named "wink" through four labelled states. The label frames
// were resolved from the SWF tag dump of character 3112:
//     idle      frame 1
//     outStart  frame 2   outEnd  frame 27   (26 ticks to cover)
//     inStart   frame 30  inEnd   frame 110  (81 ticks to reveal)
//
// The sprite's canvas is 8233x1168 with the artwork translated across it, so the frames are not
// usable as full-screen textures directly. A 689x540 strip of the curtain's leading edge was
// cropped out of frame 22 and is used as the wipe edge; the solid area is a black fill. This is the
// one visual approximation in the transition and is called out in the README.
//
// Control flow is faithful: transitionTo() queues callbacks, fadeOut() covers the screen, the
// callbacks run at outEnd (or fadeIn() when none were queued), and fadeIn() reveals then removes.
public partial class TransitionOverlay : Control
{
    private const double OutTicks = 26;    // outStart -> outEnd
    private const double InTicks = 81;     // inStart  -> inEnd

    private ColorRect _fill = null!;
    private TextureRect _edge = null!;
    private double _time;
    private bool _running;
    private bool _revealing;
    private Action? _onCovered;

    private static TransitionOverlay? FindActive(SceneTree tree)
    {
        foreach (Node child in tree.Root.GetChildren())
            if (child is TransitionOverlay overlay && !overlay.IsQueuedForDeletion())
                return overlay;
        return null;
    }

    public static bool IsActive(SceneTree tree) => FindActive(tree) != null;

    public static TransitionOverlay To(Node context, string scenePath)
    {
        var tree = context.GetTree();
        if (FindActive(tree) is { } active) return active;
        var overlay = new TransitionOverlay();
        // Parent to the root window: it survives ChangeSceneToFile, unlike the current scene.
        tree.Root.AddChild(overlay);
        overlay.PlayOut(() =>
        {
            tree.ChangeSceneToFile(scenePath);
            overlay.CallDeferred(nameof(PlayIn));
        });
        return overlay;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);
        ZIndex = 1000;
        Visible = false;

        _fill = new ColorRect
        {
            Color = new Color(0.06f, 0.06f, 0.07f),
            Position = Vector2.Zero,
            Size = Vector2.Zero,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_fill);

        _edge = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            FlipH = true,                 // the cropped strip's soft edge is on its left
            MouseFilter = MouseFilterEnum.Ignore
        };
        _edge.Texture = GD.Load<Texture2D>("res://Assets/Bonus/curtain.png");
        _edge.Size = new Vector2(689, 540);
        AddChild(_edge);

        GuiTick.Signal += OnTick;
    }

    public void PlayOut(Action? onCovered)
    {
        _onCovered = onCovered;
        _time = 0;
        _revealing = false;
        _running = true;
        Visible = true;
        Apply();
    }

    public void PlayIn()
    {
        _time = 0;
        _revealing = true;
        _running = true;
        Visible = true;
        Apply();
    }

    private void OnTick(double delta)
    {
        if (!_running) return;
        _time += delta;

        if (!_revealing)
        {
            if (_time >= OutTicks)
            {
                _running = false;
                ApplyCovered();
                var cb = _onCovered;
                _onCovered = null;
                cb?.Invoke();
                return;
            }
            Apply();
        }
        else
        {
            if (_time >= InTicks)
            {
                _running = false;
                Visible = false;
                QueueFree();
                return;
            }
            Apply();
        }
    }

    private void Apply()
    {
        var size = GetViewportRect().Size;
        if (_edge.Size.Y != size.Y)
        {
            _edge.Size = new Vector2(689f * size.Y / 540f, size.Y);
            _edge.Texture = _edge.Texture;
        }

        if (!_revealing)
        {
            // Wipe in from the left: the black area grows, the soft edge rides its leading side.
            float p = (float)Math.Clamp(_time / OutTicks, 0, 1);
            float w = size.X * p;
            _fill.Size = new Vector2(w, size.Y);
            _edge.Position = new Vector2(w - _edge.Size.X, 0);
            _edge.Modulate = new Color(1, 1, 1, Math.Min(1, p * 6));
        }
        else
        {
            // Reveal: the whole covered slab slides off to the right.
            float p = (float)Math.Clamp(_time / InTicks, 0, 1);
            float shift = size.X * p;
            _fill.Size = new Vector2(size.X, size.Y);
            _fill.Position = new Vector2(shift, 0);
            _edge.Position = new Vector2(shift + size.X - _edge.Size.X, 0);
        }
    }

    private void ApplyCovered()
    {
        var size = GetViewportRect().Size;
        _fill.Position = Vector2.Zero;
        _fill.Size = new Vector2(size.X, size.Y);
        _edge.Position = new Vector2(size.X - _edge.Size.X, 0);
    }

    public override void _ExitTree() => GuiTick.Signal -= OnTick;
}
