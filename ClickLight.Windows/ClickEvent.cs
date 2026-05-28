namespace ClickLight.Windows;

/// <summary>
/// Types of mouse click events captured by the hook.
/// </summary>
public enum ClickKind
{
    LeftDown,
    LeftUp,
    RightDown,
    RightUp,
    Drag,
    Move,
    FileDrag
}

/// <summary>
/// Represents a single captured click event with location and timing.
/// </summary>
public sealed class ClickEvent
{
    public ClickKind Kind { get; }
    public double X { get; }
    public double Y { get; }
    public double Timestamp { get; }

    /// <summary>
    /// For Drag events: the point where the drag started (physical screen pixels).
    /// </summary>
    public double DragStartX { get; }
    public double DragStartY { get; }

    public ClickEvent(ClickKind kind, double x, double y, double timestamp, double dragStartX = 0, double dragStartY = 0)
    {
        Kind = kind;
        X = x;
        Y = y;
        Timestamp = timestamp;
        DragStartX = dragStartX;
        DragStartY = dragStartY;
    }

    public bool IsRelease => Kind == ClickKind.LeftUp || Kind == ClickKind.RightUp;
}
