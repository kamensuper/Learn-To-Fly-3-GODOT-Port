using Godot;
using System;

// Translation of src.gui.decorators.TweenPos.
// Faithful details kept: time starts at -delay, an axis with a zero modifier is left untouched,
// the final frame snaps to start+mod, and the finished signal fires before the tween is destroyed.
public sealed class TweenPos
{
    public double Duration;
    public double Time;
    public Vector2 StartPoint;
    public Vector2 ModPoint;

    private GodotObject? _target;
    private Func<double, double, double, double, double> _easing = Easing.EaseBothInOutQuad;
    private bool _isFinished;
    private bool _destroyed;

    public event Action? Finished;

    public TweenPos(GodotObject? target, Vector2? start, Vector2? mod,
                    double duration = 10, double delay = 0,
                    Func<double, double, double, double, double>? easing = null)
    {
        _target = target;
        if (target == null) { Destroy(); return; }

        var node = (Node2D)target;
        StartPoint = start ?? node.Position;
        ModPoint = mod ?? Vector2.Zero;
        Duration = duration;
        Time = -delay;
        _easing = easing ?? ((t, b, c, d) => Easing.EaseBothInOutQuad(t, b, c, d));

        Tween();
        GuiTick.Signal += Tick;
    }

    // TweenPos.as:107
    public static TweenPos TweenToCurrentPosition(Node2D target, Vector2 offset,
        double duration = 10, double delay = 0,
        Func<double, double, double, double, double>? easing = null)
        => new(target, target.Position + offset, -offset, duration, delay, easing);

    // TweenPos.as:114
    public static TweenPos TweenFromCurrentPosition(Node2D target, Vector2 offset,
        double duration = 10, double delay = 0,
        Func<double, double, double, double, double>? easing = null)
        => new(target, null, offset, duration, delay, easing);

    // TweenPos.as:120  (only the axes flagged by param6/param7 are tweened)
    public static TweenPos TweenFromCurrentPosition2(Node2D target, Vector2 destination,
        double duration = 10, double delay = 0,
        Func<double, double, double, double, double>? easing = null,
        bool moveX = true, bool moveY = true)
    {
        var mod = Vector2.Zero;
        if (moveX) mod.X = destination.X - target.Position.X;
        if (moveY) mod.Y = destination.Y - target.Position.Y;
        return new TweenPos(target, null, mod, duration, delay, easing);
    }

    private void Tick(double delta)
    {
        Time += delta;
        Tween();
        if (Time >= Duration)
        {
            if (_target is Node2D node)
            {
                if (Math.Abs(ModPoint.X) > 0.000001f) node.Position = new Vector2(StartPoint.X + ModPoint.X, node.Position.Y);
                if (Math.Abs(ModPoint.Y) > 0.000001f) node.Position = new Vector2(node.Position.X, StartPoint.Y + ModPoint.Y);
            }
            _isFinished = true;
            Finished?.Invoke();
            Destroy();
        }
    }

    private void Tween()
    {
        if (_target is not Node2D node) return;
        var t = Math.Clamp(Time, 0, Duration);
        var x = node.Position.X;
        var y = node.Position.Y;
        if (Math.Abs(ModPoint.X) > 0.000001f)
            x = (float)_easing(t, StartPoint.X, ModPoint.X, Duration);
        if (Math.Abs(ModPoint.Y) > 0.000001f)
            y = (float)_easing(t, StartPoint.Y, ModPoint.Y, Duration);
        node.Position = new Vector2(x, y);
    }

    public bool IsFinished => _isFinished;

    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;
        if (_target is Node2D node && !_isFinished)
        {
            if (Math.Abs(ModPoint.X) > 0.000001f) node.Position = new Vector2(StartPoint.X + ModPoint.X, node.Position.Y);
            if (Math.Abs(ModPoint.Y) > 0.000001f) node.Position = new Vector2(node.Position.X, StartPoint.Y + ModPoint.Y);
        }
        GuiTick.Signal -= Tick;
        Finished = null;
        _target = null;
    }
}
