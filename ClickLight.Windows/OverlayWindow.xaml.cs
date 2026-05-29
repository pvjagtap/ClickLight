using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ClickLight.Windows;

/// <summary>
/// Transparent, click-through overlay window that renders click pulse animations.
/// Equivalent to ClickOverlayWindow + ClickOverlayView on macOS.
/// Uses WS_EX_TRANSPARENT to pass all input through to underlying windows.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    // Max simultaneous canvas children to prevent memory runaway from rapid clicking.
    // Must be large enough for: stroke segments (300) + trail dots (120) + active pulses.
    private const int MaxCanvasChildren = 600;

    // Laser pointer constants
    private const double LaserCursorFadeDuration = 0.42;
    private static readonly Color LaserColor = Color.FromRgb(255, 41, 61);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr newStyle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private readonly Rect _physicalBounds;
    private double _dpiScale = 1.0;

    public OverlayWindow(Rect physicalScreenBounds)
    {
        InitializeComponent();
        _physicalBounds = physicalScreenBounds;

        // Set approximate initial position (will be corrected in OnLoaded via SetWindowPos)
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = physicalScreenBounds.Left;
        Top = physicalScreenBounds.Top;
        Width = physicalScreenBounds.Width;
        Height = physicalScreenBounds.Height;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Set extended styles: click-through + tool window (no taskbar entry)
        if (IntPtr.Size == 8)
        {
            var extStyle = GetWindowLongPtr64(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr64(hwnd, GWL_EXSTYLE, (IntPtr)((long)extStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW));
        }
        else
        {
            var extStyle = GetWindowLong32(hwnd, GWL_EXSTYLE);
            SetWindowLong32(hwnd, GWL_EXSTYLE, extStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        // IMPORTANT: Position the window on its target monitor FIRST.
        // GetDpiForWindow returns the DPI of whichever monitor the window is currently on.
        // If we read DPI before moving, the window may still be on the primary monitor
        // (WPF placed it using logical coords), giving the wrong DPI for a non-primary monitor.
        SetWindowPos(
            hwnd, IntPtr.Zero,
            (int)_physicalBounds.Left, (int)_physicalBounds.Top,
            (int)_physicalBounds.Width, (int)_physicalBounds.Height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);

        // NOW read DPI — window is on its correct monitor.
        // With PerMonitorV2, WPF canvas uses DIPs. Mouse hook gives physical pixels.
        // Coordinate conversion: localDIP = (physicalAbs - screenOriginPhysical) / dpiScale
        var dpi = GetDpiForWindow(hwnd);
        if (dpi > 0)
        {
            _dpiScale = dpi / 96.0;
        }
        else
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
                _dpiScale = source.CompositionTarget.TransformToDevice.M11;
        }
    }

    // The active drag rectangle element — removed/replaced each drag event
    private System.Windows.Shapes.Rectangle? _dragRect;

    // Laser pointer state
    private System.Windows.Point? _laserCursorPoint;
    private double _laserCursorUpdatedAt;
    private Ellipse? _laserGlow;
    private Ellipse? _laserDot;
    private System.Windows.Threading.DispatcherTimer? _laserTimer;

    // Shooting star trail dots (when just moving the laser cursor)
    private readonly List<(Ellipse Dot, double CenterX, double CenterY, double BaseSize, double CreatedAt, double Lifetime)> _trailDots = new();
    private const int MaxTrailDots = 120;
    private const double TrailDotLifetime = 0.55; // trail dots when just moving
    private double _lastTrailDotTime;

    // Stroke line segments (when dragging) — dissolving connected lines
    private readonly List<(System.Windows.Shapes.Line Seg, double BaseThickness, double CreatedAt, double Lifetime)> _strokeSegments = new();
    private const int MaxStrokeSegments = 300;
    private const double StrokeSegmentLifetime = 1.8; // how long each line segment lives
    private System.Windows.Point? _lastStrokeDotPos;
    private ClickSettings? _currentLaserSettings;

    public void ShowPulse(ClickEvent clickEvent, ClickSettings settings)
    {
        // Guard against excessive element accumulation (auto-clicker / rapid fire)
        if (OverlayCanvas.Children.Count > MaxCanvasChildren)
            return;

        // Convert physical screen coordinates to WPF canvas coordinates (DIPs).
        // Mouse hook gives physical pixels. Canvas is in DIPs (1 DIP = dpiScale physical px).
        // Subtract screen origin (physical), then convert to DIPs.
        double localX = (clickEvent.X - _physicalBounds.Left) / _dpiScale;
        double localY = (clickEvent.Y - _physicalBounds.Top) / _dpiScale;

        // Laser pointer mode handling
        if (settings.ShowLaserPointer)
        {
            switch (clickEvent.Kind)
            {
                case ClickKind.Move:
                    ShowLaserCursor(localX, localY, settings);
                    return;
                case ClickKind.Drag:
                    RemoveDragRect();
                    AppendLaserPoint(localX, localY, settings);
                    return;
                case ClickKind.LeftUp:
                case ClickKind.RightUp:
                case ClickKind.MiddleUp:
                    CompleteLaserStroke();
                    break;
                case ClickKind.LeftDown:
                case ClickKind.RightDown:
                case ClickKind.MiddleDown:
                    RemoveDragRect();
                    break;
            }
        }

        // Skip pulse rendering for event types that shouldn't show
        if (!ShouldShowPulse(clickEvent.Kind, settings)) return;

        var color = GetColor(clickEvent.Kind, settings);
        var baseSize = GetSize(clickEvent.Kind, settings);
        var duration = GetDuration(clickEvent.Kind, settings);
        var intensity = Math.Max(0.15, Math.Min(1.35, settings.Intensity));

        switch (clickEvent.Kind)
        {
            case ClickKind.LeftDown:
                RemoveDragRect();
                AnimateRingPulse(localX, localY, baseSize, duration, intensity, color, showDot: true);
                break;
            case ClickKind.LeftUp:
                FadeOutDragRect(duration);
                AnimateReleasePulse(localX, localY, baseSize * 0.82, duration, intensity * 0.55, color, showDot: true);
                break;
            case ClickKind.RightDown:
                AnimateRingPulse(localX, localY, baseSize, duration, intensity, color, showCrosshair: true);
                break;
            case ClickKind.RightUp:
                AnimateReleasePulse(localX, localY, baseSize * 0.82, duration, intensity * 0.5, color, showCrosshair: true);
                break;
            case ClickKind.MiddleDown:
                AnimateRingPulse(localX, localY, baseSize, duration, intensity, color, showDiamond: true);
                break;
            case ClickKind.MiddleUp:
                AnimateReleasePulse(localX, localY, baseSize * 0.82, duration, intensity * 0.5, color, showDiamond: true);
                break;
            case ClickKind.Drag:
                UpdateDragRect(clickEvent, settings, color, intensity);
                break;
        }
    }

    private static bool ShouldShowPulse(ClickKind kind, ClickSettings settings) => kind switch
    {
        ClickKind.LeftDown => settings.ShowPress,
        ClickKind.LeftUp => settings.ShowRelease,
        ClickKind.RightDown => settings.ShowRightClick,
        ClickKind.RightUp => settings.ShowRightClick && settings.ShowRelease,
        ClickKind.MiddleDown => settings.ShowMiddleClick,
        ClickKind.MiddleUp => settings.ShowMiddleClick && settings.ShowRelease,
        ClickKind.Drag => settings.ShowDrag && !settings.ShowLaserPointer,
        ClickKind.Move => false,
        _ => true
    };

    /// <summary>
    /// Draws/updates a live rectangle from drag start to current cursor position.
    /// </summary>
    private void UpdateDragRect(ClickEvent evt, ClickSettings settings, Color color, double intensity)
    {
        double startX = (evt.DragStartX - _physicalBounds.Left) / _dpiScale;
        double startY = (evt.DragStartY - _physicalBounds.Top) / _dpiScale;
        double endX = (evt.X - _physicalBounds.Left) / _dpiScale;
        double endY = (evt.Y - _physicalBounds.Top) / _dpiScale;

        var left = Math.Min(startX, endX);
        var top = Math.Min(startY, endY);
        var width = Math.Abs(endX - startX);
        var height = Math.Abs(endY - startY);

        // Skip tiny rectangles (likely just a click, not a real drag)
        if (width < 4 && height < 4) return;

        var fillOpacity = Math.Max(0.06, intensity * 0.12);
        var strokeOpacity = Math.Max(0.3, intensity * 0.6);

        if (_dragRect == null)
        {
            _dragRect = new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(color) { Opacity = fillOpacity },
                Stroke = new SolidColorBrush(color) { Opacity = strokeOpacity },
                StrokeThickness = 1.5,
                RadiusX = 2,
                RadiusY = 2,
                IsHitTestVisible = false
            };
            OverlayCanvas.Children.Add(_dragRect);
        }
        else
        {
            // Update color in case settings changed mid-drag
            ((SolidColorBrush)_dragRect.Fill!).Color = color;
            ((SolidColorBrush)_dragRect.Fill!).Opacity = fillOpacity;
            ((SolidColorBrush)_dragRect.Stroke!).Color = color;
            ((SolidColorBrush)_dragRect.Stroke!).Opacity = strokeOpacity;
        }

        _dragRect.Width = width;
        _dragRect.Height = height;
        System.Windows.Controls.Canvas.SetLeft(_dragRect, left);
        System.Windows.Controls.Canvas.SetTop(_dragRect, top);
    }

    private void RemoveDragRect()
    {
        if (_dragRect != null)
        {
            OverlayCanvas.Children.Remove(_dragRect);
            _dragRect = null;
        }
    }

    private void FadeOutDragRect(double duration)
    {
        if (_dragRect == null) return;
        var rect = _dragRect;
        _dragRect = null;

        var fadeAnim = new DoubleAnimation(rect.Opacity, 0, TimeSpan.FromSeconds(duration * 0.6))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        fadeAnim.Completed += (_, _) => OverlayCanvas.Children.Remove(rect);
        rect.BeginAnimation(OpacityProperty, fadeAnim);
    }

    // ─── Laser Pointer Methods ────────────────────────────────────────────

    private void ShowLaserCursor(double x, double y, ClickSettings settings)
    {
        _laserCursorPoint = new System.Windows.Point(x, y);
        _laserCursorUpdatedAt = GetTime();
        _currentLaserSettings = settings;

        var dotSize = settings.LaserPointerSize;
        var glowSize = dotSize * 2.3;

        if (_laserGlow == null)
        {
            _laserGlow = new Ellipse
            {
                Width = glowSize, Height = glowSize,
                Fill = new SolidColorBrush(LaserColor) { Opacity = 0.18 },
                IsHitTestVisible = false
            };
            OverlayCanvas.Children.Add(_laserGlow);
        }
        else
        {
            _laserGlow.Width = glowSize;
            _laserGlow.Height = glowSize;
        }
        if (_laserDot == null)
        {
            _laserDot = new Ellipse
            {
                Width = dotSize, Height = dotSize,
                Fill = new SolidColorBrush(LaserColor),
                IsHitTestVisible = false
            };
            OverlayCanvas.Children.Add(_laserDot);
        }
        else
        {
            _laserDot.Width = dotSize;
            _laserDot.Height = dotSize;
        }

        System.Windows.Controls.Canvas.SetLeft(_laserGlow, x - glowSize / 2);
        System.Windows.Controls.Canvas.SetTop(_laserGlow, y - glowSize / 2);
        System.Windows.Controls.Canvas.SetLeft(_laserDot, x - dotSize / 2);
        System.Windows.Controls.Canvas.SetTop(_laserDot, y - dotSize / 2);
        _laserGlow.Opacity = 1;
        _laserDot.Opacity = 1;

        // Emit shooting star trail dot
        if (settings.ShootingStarTrail)
        {
            var now = GetTime();
            if (now - _lastTrailDotTime > 0.018) // ~55fps trail emission
            {
                _lastTrailDotTime = now;
                EmitTrailDot(x, y, dotSize);
            }
        }

        StartLaserTimer();
    }

    private void EmitTrailDot(double x, double y, double baseSize)
    {
        // Limit trail dots to prevent memory runaway
        if (_trailDots.Count >= MaxTrailDots)
        {
            var (oldDot, _, _, _, _, _) = _trailDots[0];
            OverlayCanvas.Children.Remove(oldDot);
            _trailDots.RemoveAt(0);
        }

        // Create a trail dot slightly smaller than the main dot
        var trailSize = baseSize * 0.55;
        var trailDot = new Ellipse
        {
            Width = trailSize,
            Height = trailSize,
            Fill = new SolidColorBrush(LaserColor) { Opacity = 0.7 },
            IsHitTestVisible = false
        };

        System.Windows.Controls.Canvas.SetLeft(trailDot, x - trailSize / 2);
        System.Windows.Controls.Canvas.SetTop(trailDot, y - trailSize / 2);
        OverlayCanvas.Children.Add(trailDot);
        _trailDots.Add((trailDot, x, y, trailSize, GetTime(), TrailDotLifetime));
    }

    private void AppendLaserPoint(double x, double y, ClickSettings settings)
    {
        ShowLaserCursor(x, y, settings);

        var point = new System.Windows.Point(x, y);
        var strokeThickness = settings.LaserPointerSize * 0.7; // line thickness proportional to pointer

        if (_lastStrokeDotPos == null)
        {
            _lastStrokeDotPos = point;
        }
        else
        {
            var last = _lastStrokeDotPos.Value;
            var dist = Math.Sqrt(Math.Pow(last.X - x, 2) + Math.Pow(last.Y - y, 2));
            // Only emit a segment if we moved at least 1 pixel (avoids zero-length lines)
            if (dist >= 1.0)
            {
                EmitStrokeSegment(last.X, last.Y, x, y, strokeThickness);
                _lastStrokeDotPos = point;
            }
        }
    }

    private void EmitStrokeSegment(double x1, double y1, double x2, double y2, double thickness)
    {
        // Remove oldest if at limit
        if (_strokeSegments.Count >= MaxStrokeSegments)
        {
            var (oldSeg, _, _, _) = _strokeSegments[0];
            OverlayCanvas.Children.Remove(oldSeg);
            _strokeSegments.RemoveAt(0);
        }

        var seg = new System.Windows.Shapes.Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = new SolidColorBrush(LaserColor) { Opacity = 0.9 },
            StrokeThickness = thickness,
            StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
            StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
            IsHitTestVisible = false
        };

        OverlayCanvas.Children.Add(seg);
        _strokeSegments.Add((seg, thickness, GetTime(), StrokeSegmentLifetime));
        StartLaserTimer();
    }

    private void CompleteLaserStroke()
    {
        // Just reset the last stroke position — existing dots will dissolve on their own
        _lastStrokeDotPos = null;
        StartLaserTimer();
    }

    private void StartLaserTimer()
    {
        if (_laserTimer != null) return;
        _laserTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _laserTimer.Tick += LaserTimerTick;
        _laserTimer.Start();
    }

    private void LaserTimerTick(object? sender, EventArgs e)
    {
        var now = GetTime();
        var anyActive = false;

        // Fade laser cursor
        if (_laserCursorPoint.HasValue)
        {
            var elapsed = now - _laserCursorUpdatedAt;
            if (elapsed >= LaserCursorFadeDuration)
            {
                RemoveLaserCursor();
            }
            else
            {
                var alpha = 1.0 - (elapsed / LaserCursorFadeDuration);
                if (_laserGlow != null) _laserGlow.Opacity = alpha * 0.18;
                if (_laserDot != null) _laserDot.Opacity = alpha;
                anyActive = true;
            }
        }

        // Fade and shrink all trail dots (shooting star effect for cursor movement)
        for (int i = _trailDots.Count - 1; i >= 0; i--)
        {
            var (dot, cx, cy, baseSize, createdAt, lifetime) = _trailDots[i];
            var elapsed = now - createdAt;
            if (elapsed >= lifetime)
            {
                OverlayCanvas.Children.Remove(dot);
                _trailDots.RemoveAt(i);
            }
            else
            {
                var progress = elapsed / lifetime;
                var scale = Math.Max(0.02, 1.0 - progress); // shrinks from full to ~0
                var alpha = (1.0 - progress) * 0.9;

                var newSize = baseSize * scale;
                dot.Width = newSize;
                dot.Height = newSize;
                dot.Opacity = alpha;

                // Reposition to keep centered as it shrinks
                System.Windows.Controls.Canvas.SetLeft(dot, cx - newSize / 2);
                System.Windows.Controls.Canvas.SetTop(dot, cy - newSize / 2);
                anyActive = true;
            }
        }

        // Fade and thin stroke line segments (dissolving drawing lines)
        for (int i = _strokeSegments.Count - 1; i >= 0; i--)
        {
            var (seg, baseThickness, createdAt, lifetime) = _strokeSegments[i];
            var elapsed = now - createdAt;
            if (elapsed >= lifetime)
            {
                OverlayCanvas.Children.Remove(seg);
                _strokeSegments.RemoveAt(i);
            }
            else
            {
                var progress = elapsed / lifetime;
                var alpha = (1.0 - progress) * 0.9;
                var thickness = baseThickness * Math.Max(0.05, 1.0 - progress); // thins out

                seg.StrokeThickness = thickness;
                if (seg.Stroke is SolidColorBrush brush)
                    seg.Stroke = new SolidColorBrush(brush.Color) { Opacity = alpha };
                anyActive = true;
            }
        }

        // Keep timer running if anything active
        if (_trailDots.Count > 0 || _strokeSegments.Count > 0)
            anyActive = true;

        if (!anyActive)
        {
            _laserTimer?.Stop();
            _laserTimer = null;
        }
    }

    private void RemoveLaserCursor()
    {
        if (_laserGlow != null)
        {
            OverlayCanvas.Children.Remove(_laserGlow);
            _laserGlow = null;
        }
        if (_laserDot != null)
        {
            OverlayCanvas.Children.Remove(_laserDot);
            _laserDot = null;
        }
        _laserCursorPoint = null;
    }

    /// <summary>Clears all laser pointer visuals (called when mode is toggled off).</summary>
    public void ClearLaser()
    {
        RemoveLaserCursor();
        _lastStrokeDotPos = null;
        foreach (var (dot, _, _, _, _, _) in _trailDots)
            OverlayCanvas.Children.Remove(dot);
        _trailDots.Clear();
        foreach (var (seg, _, _, _) in _strokeSegments)
            OverlayCanvas.Children.Remove(seg);
        _strokeSegments.Clear();
        _laserTimer?.Stop();
        _laserTimer = null;
    }

    private static double GetTime() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private void AnimateRingPulse(double x, double y, double size, double duration, double intensity, Color color, bool showDot = false, bool showCrosshair = false, bool showDiamond = false)
    {
        var startRadius = size * 0.18;
        var endRadius = size * 0.8;
        var lineWidth = Math.Max(2.25, size * (0.035 + intensity * 0.045));

        // Outer glow (only at high intensity)
        if (intensity >= 0.7)
        {
            var glow = CreateEllipse(x, y, endRadius * 1.3, 0, color, intensity * 0.12);
            AnimateExpand(glow, x, y, startRadius * 1.5, endRadius * 1.3, duration, fadeOut: true);
        }

        // Main ring
        var ring = CreateEllipse(x, y, startRadius, lineWidth, color, 0.18 + intensity * 0.78);
        AnimateExpand(ring, x, y, startRadius, endRadius, duration, fadeOut: true);

        // Center dot
        if (showDot)
        {
            var dot = CreateFilledEllipse(x, y, size * 0.085, color, (0.18 + intensity * 0.78) * 0.75);
            AnimateFadeOut(dot, duration);
        }

        // Crosshair for right-click
        if (showCrosshair)
        {
            AnimateCrosshair(x, y, size * 0.28, duration, color, (0.18 + intensity * 0.78) * 0.85);
        }

        // Diamond for middle-click
        if (showDiamond)
        {
            AnimateDiamond(x, y, size * 0.24, duration, color, (0.18 + intensity * 0.78) * 0.86);
        }
    }

    private void AnimateReleasePulse(double x, double y, double size, double duration, double intensity, Color color, bool showDot = false, bool showCrosshair = false, bool showDiamond = false)
    {
        var startRadius = size * 0.76;
        var endRadius = size * 0.34;
        var lineWidth = Math.Max(2.25, size * 0.035) * 0.55;

        var ring = CreateEllipse(x, y, startRadius, lineWidth, color, intensity);
        AnimateShrink(ring, x, y, startRadius, endRadius, duration, fadeOut: true);

        if (showDot)
        {
            var dot = CreateFilledEllipse(x, y, size * 0.055, color, intensity * 0.6);
            AnimateFadeOut(dot, duration);
        }

        if (showCrosshair)
        {
            AnimateCrosshair(x, y, size * 0.2, duration, color, intensity * 0.7);
        }

        if (showDiamond)
        {
            AnimateDiamond(x, y, size * 0.18, duration, color, intensity * 0.82);
        }
    }

    private void AnimateCrosshair(double x, double y, double size, double duration, Color color, double opacity)
    {
        var brush = new SolidColorBrush(color);
        var lineWidth = Math.Max(2, size * 0.12);

        var hLine = new System.Windows.Shapes.Line
        {
            X1 = x - size, Y1 = y, X2 = x + size, Y2 = y,
            Stroke = brush, StrokeThickness = lineWidth,
            Opacity = opacity, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        var vLine = new System.Windows.Shapes.Line
        {
            X1 = x, Y1 = y - size, X2 = x, Y2 = y + size,
            Stroke = brush, StrokeThickness = lineWidth,
            Opacity = opacity, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };

        OverlayCanvas.Children.Add(hLine);
        OverlayCanvas.Children.Add(vLine);

        AnimateFadeOut(hLine, duration);
        AnimateFadeOut(vLine, duration);
    }

    private void AnimateDiamond(double x, double y, double size, double duration, Color color, double opacity)
    {
        var diamond = new System.Windows.Shapes.Polygon
        {
            Points = new PointCollection
            {
                new System.Windows.Point(x, y - size),
                new System.Windows.Point(x + size, y),
                new System.Windows.Point(x, y + size),
                new System.Windows.Point(x - size, y)
            },
            Fill = new SolidColorBrush(color),
            Opacity = Math.Max(0, Math.Min(1, opacity)),
            IsHitTestVisible = false
        };
        OverlayCanvas.Children.Add(diamond);
        AnimateFadeOut(diamond, duration);
    }

    private Ellipse CreateEllipse(double cx, double cy, double radius, double strokeWidth, Color color, double opacity)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = strokeWidth,
            Fill = System.Windows.Media.Brushes.Transparent,
            Opacity = Math.Max(0, Math.Min(1, opacity)),
            IsHitTestVisible = false
        };
        System.Windows.Controls.Canvas.SetLeft(ellipse, cx - radius);
        System.Windows.Controls.Canvas.SetTop(ellipse, cy - radius);
        OverlayCanvas.Children.Add(ellipse);
        return ellipse;
    }

    private Ellipse CreateFilledEllipse(double cx, double cy, double radius, Color color, double opacity)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new SolidColorBrush(color),
            Opacity = Math.Max(0, Math.Min(1, opacity)),
            IsHitTestVisible = false
        };
        System.Windows.Controls.Canvas.SetLeft(ellipse, cx - radius);
        System.Windows.Controls.Canvas.SetTop(ellipse, cy - radius);
        OverlayCanvas.Children.Add(ellipse);
        return ellipse;
    }

    private void AnimateExpand(Ellipse ellipse, double cx, double cy, double fromRadius, double toRadius, double duration, bool fadeOut)
    {
        var dur = TimeSpan.FromSeconds(duration);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var widthAnim = new DoubleAnimation(fromRadius * 2, toRadius * 2, dur) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(fromRadius * 2, toRadius * 2, dur) { EasingFunction = ease };
        var leftAnim = new DoubleAnimation(cx - fromRadius, cx - toRadius, dur) { EasingFunction = ease };
        var topAnim = new DoubleAnimation(cy - fromRadius, cy - toRadius, dur) { EasingFunction = ease };

        ellipse.BeginAnimation(WidthProperty, widthAnim);
        ellipse.BeginAnimation(HeightProperty, heightAnim);
        ellipse.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, leftAnim);
        ellipse.BeginAnimation(System.Windows.Controls.Canvas.TopProperty, topAnim);

        if (fadeOut)
        {
            var fadeAnim = new DoubleAnimation(ellipse.Opacity, 0, dur) { EasingFunction = ease };
            fadeAnim.Completed += (_, _) => OverlayCanvas.Children.Remove(ellipse);
            ellipse.BeginAnimation(OpacityProperty, fadeAnim);
        }
    }

    private void AnimateShrink(Ellipse ellipse, double cx, double cy, double fromRadius, double toRadius, double duration, bool fadeOut)
    {
        var dur = TimeSpan.FromSeconds(duration);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var widthAnim = new DoubleAnimation(fromRadius * 2, toRadius * 2, dur) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(fromRadius * 2, toRadius * 2, dur) { EasingFunction = ease };
        var leftAnim = new DoubleAnimation(cx - fromRadius, cx - toRadius, dur) { EasingFunction = ease };
        var topAnim = new DoubleAnimation(cy - fromRadius, cy - toRadius, dur) { EasingFunction = ease };

        ellipse.BeginAnimation(WidthProperty, widthAnim);
        ellipse.BeginAnimation(HeightProperty, heightAnim);
        ellipse.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, leftAnim);
        ellipse.BeginAnimation(System.Windows.Controls.Canvas.TopProperty, topAnim);

        if (fadeOut)
        {
            var fadeAnim = new DoubleAnimation(ellipse.Opacity, 0, dur) { EasingFunction = ease };
            fadeAnim.Completed += (_, _) => OverlayCanvas.Children.Remove(ellipse);
            ellipse.BeginAnimation(OpacityProperty, fadeAnim);
        }
    }

    private void AnimateFadeOut(UIElement element, double duration)
    {
        var dur = TimeSpan.FromSeconds(duration);
        var fadeAnim = new DoubleAnimation(element.Opacity, 0, dur)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        fadeAnim.Completed += (_, _) => OverlayCanvas.Children.Remove(element);
        element.BeginAnimation(OpacityProperty, fadeAnim);
    }

    private static Color GetColor(ClickKind kind, ClickSettings settings)
    {
        if (settings.ColorPreset == ColorPreset.Custom)
        {
            return settings.CustomColorMode switch
            {
                CustomColorMode.ByClick => kind switch
                {
                    ClickKind.LeftDown or ClickKind.LeftUp => settings.CustomLeftColor,
                    ClickKind.RightDown or ClickKind.RightUp => settings.CustomRightColor,
                    ClickKind.MiddleDown or ClickKind.MiddleUp => settings.CustomMiddleColor,
                    ClickKind.Drag => settings.CustomDragColor,
                    _ => settings.CustomColor
                },
                _ => settings.CustomColor
            };
        }

        var presetColor = settings.ColorPreset.GetColor();
        if (presetColor.HasValue)
            return presetColor.Value;

        return kind switch
        {
            ClickKind.LeftDown => Color.FromRgb(0, 189, 255),
            ClickKind.LeftUp => Color.FromRgb(102, 224, 255),
            ClickKind.RightDown or ClickKind.RightUp => Color.FromRgb(255, 117, 48),
            ClickKind.MiddleDown or ClickKind.MiddleUp => Color.FromRgb(69, 235, 148),
            ClickKind.Drag => Color.FromRgb(235, 214, 56),
            ClickKind.Move => Colors.Transparent,
            _ => Color.FromRgb(0, 189, 255)
        };
    }

    private static double GetSize(ClickKind kind, ClickSettings settings) => kind switch
    {
        ClickKind.Drag => settings.Size * 0.6,
        ClickKind.Move => 0,
        ClickKind.LeftUp or ClickKind.RightUp or ClickKind.MiddleUp => settings.Size * 0.82,
        _ => settings.Size
    };

    private static double GetDuration(ClickKind kind, ClickSettings settings) => kind switch
    {
        ClickKind.Drag => Math.Min(0.38, settings.Duration * 0.82),
        ClickKind.Move => 0,
        ClickKind.LeftUp or ClickKind.RightUp or ClickKind.MiddleUp => settings.Duration * 0.78,
        _ => settings.Duration
    };
}
