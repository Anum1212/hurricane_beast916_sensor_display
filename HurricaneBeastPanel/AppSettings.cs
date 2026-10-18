using System.Text.Json;

namespace HurricaneBeastPanel;

public sealed class AppSettings
{
    /// <summary>COM port name, empty means auto-detect.</summary>
    public string Port { get; set; } = "";
    /// <summary>Limits screen detection to one brand (the factory's Brand, e.g. "Hurricane Beast"). Empty means try every driver.</summary>
    public string Driver { get; set; } = "";
    public int Brightness { get; set; } = 80;
    /// <summary>How many times per second the current frame is written to the screen.</summary>
    public int SendFps { get; set; } = 15;
    public bool Fahrenheit { get; set; }
    /// <summary>Theme editor: draw a grid over the canvas (design time only, never sent to the screen).</summary>
    public bool ShowGrid { get; set; }
    /// <summary>Theme editor: moving and resizing snaps to the grid.</summary>
    public bool SnapToGrid { get; set; }
    /// <summary>Distance between grid lines in theme pixels.</summary>
    public int GridSize { get; set; } = 20;
    /// <summary>City for the weather elements, e.g. "Paris" or "Paris, France". Empty means find it from the internet connection.</summary>
    public string WeatherLocation { get; set; } = "";
    /// <summary>Turns the image upside-down on the panel (swaps between the two possible rotations).</summary>
    public bool Flip180 { get; set; }
    public string ThemeName { get; set; } = "Default";
    /// <summary>Set once the built-in vertical theme has been written to disk.</summary>
    public bool VerticalThemeAdded { get; set; }
    /// <summary>Set once the built-in Neon Dashboard theme has been written to disk.</summary>
    public bool NeonThemeAdded { get; set; }
    /// <summary>Version of the extra built-in theme pack that has been written to disk (so each pack is added once).</summary>
    public int ExtraThemesVersion { get; set; }
    public bool StartMinimized { get; set; }
    public bool AutoConnect { get; set; } = true;

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HurricaneBeastPanel");

    static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* fall back to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* non-fatal */ }
    }
}

static class Log
{
    static readonly object Lock = new();

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(AppSettings.Dir);
                string path = Path.Combine(AppSettings.Dir, "log.txt");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
