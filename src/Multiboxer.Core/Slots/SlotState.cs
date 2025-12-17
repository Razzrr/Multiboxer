namespace Multiboxer.Core.Slots;

/// <summary>
/// Represents the current state of a slot
/// </summary>
public enum SlotState
{
    /// <summary>
    /// Slot is empty, no process assigned
    /// </summary>
    Empty,

    /// <summary>
    /// Process is starting up
    /// </summary>
    Starting,

    /// <summary>
    /// Process is running and window is available
    /// </summary>
    Running,

    /// <summary>
    /// Window is currently in foreground (active)
    /// </summary>
    Foreground,

    /// <summary>
    /// Process is running but minimized
    /// </summary>
    Minimized,

    /// <summary>
    /// Process has exited
    /// </summary>
    Exited,

    /// <summary>
    /// Error occurred with this slot
    /// </summary>
    Error
}
