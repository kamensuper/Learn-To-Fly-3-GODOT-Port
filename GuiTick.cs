using System;

// Translation of src.gui.GuiSettings.guiTickSignal.
// One global tick source; every tween/bounce subscribes and receives a tick delta.
public static class GuiTick
{
    public static event Action<double>? Signal;

    // Called once per frame by the active screen (replaces Main's global dispatch).
    public static void Pump(double deltaSeconds) => Emit(deltaSeconds * 60.0);

    public static void Emit(double ticks)
    {
        var signal = Signal;
        if (signal == null) return;
        // Snapshot the list: handlers routinely unsubscribe from inside their own callback.
        foreach (var d in signal.GetInvocationList()) ((Action<double>)d)(ticks);
    }
}
