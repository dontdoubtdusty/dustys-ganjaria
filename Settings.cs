using Raykit;

namespace DustysGanjaria;

// Player settings, kept apart from the save so starting over (deleting save.json) keeps them. Saved as JSON in
// %APPDATA%\DustysGanjaria\settings.json whenever the pause menu changes one (Raykit's JsonStore: safe write,
// a broken file is kept as settings.json.bad and the defaults are used).
sealed class Settings
{
    // Master volume, 0 to 1.
    public float Volume { get; set; } = 0.8f;
    // Full screen (borderless, the monitor's size) instead of a 1280 x 720 window.
    public bool Fullscreen { get; set; }

    static string FilePath => Path.Combine(Paths.AppData("DustysGanjaria"), "settings.json");

    // Loads the settings, or the defaults if there are none or they cannot be read.
    public static Settings Load()
    {
        var s = JsonStore.Load<Settings>(FilePath);
        s.Volume = Math.Clamp(s.Volume, 0f, 1f);
        return s;
    }

    // Writes the settings. A failure is ignored: they just will not be remembered.
    public void Save() => JsonStore.Save(FilePath, this);
}
