using System.Text.Json;

namespace DustysGanjaria;

// Player settings, kept apart from the save so starting over (deleting save.json) keeps them. Saved as JSON in
// %APPDATA%\DustysGanjaria\settings.json whenever the pause menu changes one.
sealed class Settings
{
    // Master volume, 0 to 1.
    public float Volume { get; set; } = 0.8f;
    // Full screen (borderless, the monitor's size) instead of a 1280 x 720 window.
    public bool Fullscreen { get; set; }

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DustysGanjaria", "settings.json");

    // Loads the settings, or the defaults if there are none or they cannot be read.
    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
                s.Volume = Math.Clamp(s.Volume, 0f, 1f);
                return s;
            }
        }
        catch (Exception) { }
        return new Settings();
    }

    // Writes the settings. A failure is ignored: they just will not be remembered.
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { }
    }
}
