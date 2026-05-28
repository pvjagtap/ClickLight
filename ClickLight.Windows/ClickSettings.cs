using System.Text.Json;
using System.Text.Json.Serialization;
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
    public bool ShowMiddleClick { get; set; } = true;
    public bool ShowDrag { get; set; } = true;
    public bool ShowLaserPointer { get; set; } = false;
    public double Size { get; set; } = 64;
    public double Intensity { get; set; } = 0.7;
    public double Duration { get; set; } = 0.48;
    public ColorPreset ColorPreset { get; set; } = ColorPreset.Default;
    public CustomColorMode CustomColorMode { get; set; } = CustomColorMode.All;

    [JsonConverter(typeof(HexColorConverter))]
    public Color CustomColor { get; set; } = Color.FromRgb(0, 189, 255);

    [JsonConverter(typeof(HexColorConverter))]
    public Color CustomLeftColor { get; set; } = Color.FromRgb(0, 189, 255);

    [JsonConverter(typeof(HexColorConverter))]
    public Color CustomRightColor { get; set; } = Color.FromRgb(255, 117, 48);

    [JsonConverter(typeof(HexColorConverter))]
    public Color CustomMiddleColor { get; set; } = Color.FromRgb(69, 235, 148);

    [JsonConverter(typeof(HexColorConverter))]
    public Color CustomDragColor { get; set; } = Color.FromRgb(235, 214, 56);

    public ClickSettings Clone() => (ClickSettings)MemberwiseClone();
}

/// <summary>
/// Serializes System.Windows.Media.Color as "#RRGGBB" hex string.
/// Also handles the old bloated object format for backward compatibility.
/// </summary>
public class HexColorConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var hex = reader.GetString()!.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex[..2], 16);
                var g = Convert.ToByte(hex[2..4], 16);
                var b = Convert.ToByte(hex[4..6], 16);
                return Color.FromRgb(r, g, b);
            }
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Handle old format: { "A": 255, "R": 0, "G": 189, "B": 255, "ScR": ..., ... }
            byte r = 0, g = 0, b = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString();
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out var val))
                    {
                        switch (prop)
                        {
                            case "R": r = val; break;
                            case "G": g = val; break;
                            case "B": b = val; break;
                        }
                    }
                }
            }
            return Color.FromRgb(r, g, b);
        }
        return Color.FromRgb(0, 189, 255); // fallback
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"#{value.R:X2}{value.G:X2}{value.B:X2}");
    }
}

/// <summary>
/// When ColorPreset is Custom, determines whether one color applies to all events
/// or separate colors apply per click type.
/// </summary>
public enum CustomColorMode
{
    All,
    ByClick
}

public static class CustomColorModeExtensions
{
    public static string DisplayName(this CustomColorMode mode) => mode switch
    {
        CustomColorMode.All => "One Color",
        CustomColorMode.ByClick => "By Click",
        _ => mode.ToString()
    };
}

public enum ColorPreset
{
    Default,
    Primary,
    Blue,
    Green,
    Purple,
    Pink,
    Orange,
    White,
    Custom
}

public static class ColorPresetExtensions
{
    public static string DisplayName(this ColorPreset preset) => preset switch
    {
        ColorPreset.Default => "Default",
        ColorPreset.Primary => "Primary (Accent)",
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
        ColorPreset.Primary => GetSystemAccentColor(),
        ColorPreset.Blue => Color.FromRgb(0, 189, 255),
        ColorPreset.Green => Color.FromRgb(51, 230, 107),
        ColorPreset.Purple => Color.FromRgb(148, 92, 255),
        ColorPreset.Pink => Color.FromRgb(255, 82, 184),
        ColorPreset.Orange => Color.FromRgb(255, 117, 48),
        ColorPreset.White => Color.FromRgb(255, 255, 255),
        _ => null
    };

    private static Color GetSystemAccentColor()
    {
        try
        {
            // Read Windows accent color from registry
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int accentInt)
            {
                // ABGR format
                var a = (byte)((accentInt >> 24) & 0xFF);
                var b = (byte)((accentInt >> 16) & 0xFF);
                var g = (byte)((accentInt >> 8) & 0xFF);
                var r = (byte)(accentInt & 0xFF);
                return Color.FromRgb(r, g, b);
            }
        }
        catch { }
        // Fallback: Windows blue accent
        return Color.FromRgb(0, 120, 215);
    }
}
