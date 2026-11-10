using Godot;
using System;

// Translation of src.gui.decorators.MouseBounce2.
// The SWF export proves every button has identical up/over/down art, so all feedback is scale only:
//   hover in  : durationIn  = 10 ticks, EaseOutBack, +scaleMod on the initial scale
//   hover out : durationOut = 15 ticks, EaseBothInOutQuad, back to the initial scale
//   press     : initial scale - scaleMod * 0.5
// ColourMatrix effects are omitted because MainMenu never sets colorWhenOver/colorWhenDown.
public sealed class MouseBounce2
{
    public double ScaleMod = 0.1;
    public double DurationIn = 10;
    public double DurationOut = 15;
    public bool EnableMouseDownAnim = true;

    private Control? _target;
    private float _initialScaleX = 1f, _initialScaleY = 1f;
    private float _scaleOutStartX, _scaleOutStartY;
    private double _animTime;
    private bool _mouseIsOver, _mouseIsDown, _enabled = true;
    private bool _wasDown;
    private bool _destroyed;

    public event Action<Control>? SignalOver;
    public event Action<Control>? SignalOut;
    public event Action<Control>? SignalClick;

    public MouseBounce2(Control target)
    {
        _target = target;
        _initialScaleX = target.Scale.X;
        _initialScaleY = target.Scale.Y;
        _scaleOutStartX = _initialScaleX;
        _scaleOutStartY = _initialScaleY;
        _animTime = DurationOut;
        GuiTick.Signal += Tick;
    }

    private void Tick(double delta)
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target)) { Destroy(); return; }
        if (!_enabled) return;

        var over = HitTest();
        var down = Input.IsMouseButtonPressed(MouseButton.Left);

        if (over && !_mouseIsOver)
        {
            SignalOver?.Invoke(_target);
            _mouseIsOver = true;
            _animTime = 0;
            _scaleOutStartX = _target.Scale.X;
            _scaleOutStartY = _target.Scale.Y;
        }
        else if (!over && _mouseIsOver)
        {
            SignalOut?.Invoke(_target);
            _mouseIsOver = false;
            _animTime = 0;
            _scaleOutStartX = _target.Scale.X;
            _scaleOutStartY = _target.Scale.Y;
        }

        if (down && !_wasDown && _mouseIsOver && EnableMouseDownAnim)
        {
            _mouseIsDown = true;
            _target.Scale = new Vector2(
                (float)(_initialScaleX - ScaleMod * 0.5),
                (float)(_initialScaleY - ScaleMod * 0.5));
        }
        else if (!down && _wasDown && _mouseIsDown)
        {
            _mouseIsDown = false;
            // MouseBounce2.as:mouseUp - snap to the hovered scale, then fire if the pointer is still inside.
            _target.Scale = new Vector2((float)(_initialScaleX + ScaleMod), (float)(_initialScaleY + ScaleMod));
            if (HitTest()) SignalClick?.Invoke(_target);
            else _target.Scale = new Vector2(_scaleOutStartX, _scaleOutStartY);
            _mouseIsOver = HitTest();
            _animTime = 0;
            _scaleOutStartX = _target.Scale.X;
            _scaleOutStartY = _target.Scale.Y;
        }
        _wasDown = down;

        if (!_mouseIsDown)
        {
            if (_mouseIsOver && _animTime < DurationIn)
            {
                _animTime += delta;
                var s = Easing.EaseOutBack(_animTime, _initialScaleX, ScaleMod, DurationIn);
                _target.Scale = new Vector2((float)s, (float)s);
            }
            else if (!_mouseIsOver && _animTime < DurationOut)
            {
                _animTime += delta;
                var sx = Easing.EaseBothInOutQuad(_animTime, _scaleOutStartX, -(_scaleOutStartX - _initialScaleX), DurationOut);
                var sy = Easing.EaseBothInOutQuad(_animTime, _scaleOutStartY, -(_scaleOutStartY - _initialScaleY), DurationOut);
                _target.Scale = new Vector2((float)sx, (float)sy);
            }
        }
    }

    private bool HitTest()
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target)) return false;
        // Guard: a control that is not in the tree has no viewport to resolve the mouse against.
        if (!_target.IsInsideTree()) return false;
        return _target.GetGlobalRect().HasPoint(_target.GetGlobalMousePosition());
    }

    public bool MouseIsOver => _mouseIsOver;

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; }
    }

    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;
        GuiTick.Signal -= Tick;
        SignalOver = null; SignalOut = null; SignalClick = null;
        _target = null;
    }
}
