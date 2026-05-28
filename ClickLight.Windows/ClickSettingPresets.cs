namespace ClickLight.Windows;

/// <summary>
/// Named presets for size, duration, and intensity — mirrors macOS ClickSettingOptions.
/// </summary>
public static class ClickSettingPresets
{
    public record Preset(string Title, double Value);

    public static readonly Preset[] SizePresets =
    [
        new("Small", 44),
        new("Medium", 64),
        new("Large", 88),
        new("Huge", 116)
    ];

    public static readonly Preset[] IntensityPresets =
    [
        new("Subtle", 0.28),
        new("Normal", 0.7),
        new("Bright", 1.0),
        new("Beacon", 1.35)
    ];

    public static readonly Preset[] DurationPresets =
    [
        new("Snappy", 0.28),
        new("Normal", 0.48),
        new("Slow", 0.72),
        new("Very Slow", 1.0)
    ];
}
