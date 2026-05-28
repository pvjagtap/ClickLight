using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClickLight.Windows;

/// <summary>
/// Persists ClickSettings to a JSON file in AppData. Raises SettingsChanged when updated.
/// </summary>
public sealed class SettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClickLight",
        "settings.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ClickSettings Settings { get; private set; }
    public event Action? SettingsChanged;

    public SettingsStore()
    {
        Settings = Load();
    }

    public void Update(Action<ClickSettings> mutate)
    {
        mutate(Settings);
        Save();
        SettingsChanged?.Invoke();
    }

    public void Reset()
    {
        Settings = new ClickSettings();
        Save();
        SettingsChanged?.Invoke();
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static ClickSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ClickSettings>(json) ?? new ClickSettings();
            }
        }
        catch
        {
            // Corrupted file - return defaults
        }
        return new ClickSettings();
    }
}
