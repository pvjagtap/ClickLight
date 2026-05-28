using System.Windows.Media;

namespace ClickLight.Windows;

/// <summary>
/// All user-configurable settings for ClickLight, mirroring the macOS version.
/// </summary>
public class ClickSettings
{
    public bool IsEnabled { get; set; } = true;
    public bool ShowPress { get; set; } = true;
    public bool ShowRelease { get; set; } = true;
    public bool ShowRightClick { get; set; } = true;
    public bool ShowDrag { get; set; } = true;
    public bool ShowLaserPointer { get; set; } = false;
    public double Size { get; set; } = 64;
    public double Intensity { get; set; } = 0.7;
    public double Duration { get; set; } = 0.48;
    public ColorPreset ColorPreset { get; set; } = ColorPreset.Default;
    public Color CustomColor { get; set; } = Color.FromRgb(0, 189, 255);

    public ClickSettings Clone() => (ClickSettings)MemberwiseClone();
}

public enum ColorPreset
{
    Default,
    Custom,
    Blue,
    Green,
    Purple,
    Pink,
    Orange,
    White
}

public static class ColorPresetExtensions
{
    public static string DisplayName(this ColorPreset preset) => preset switch
    {
        ColorPreset.Default => "Default",
        ColorPreset.Custom => "Custom",
        ColorPreset.Blue => "Blue",
        ColorPreset.Green => "Green",
        ColorPreset.Purple => "Purple",
        ColorPreset.Pink => "Pink",
        ColorPreset.Orange => "Orange",
        ColorPreset.White => "White",
        _ => preset.ToString()
    };

    public static Color? GetColor(this ColorPreset preset) => preset switch
    {
        ColorPreset.Blue => Color.FromRgb(0, 189, 255),
        ColorPreset.Green => Color.FromRgb(51, 230, 107),
        ColorPreset.Purple => Color.FromRgb(148, 92, 255),
        ColorPreset.Pink => Color.FromRgb(255, 82, 184),
        ColorPreset.Orange => Color.FromRgb(255, 117, 48),
        ColorPreset.White => Color.FromRgb(255, 255, 255),
        _ => null
    };
}
