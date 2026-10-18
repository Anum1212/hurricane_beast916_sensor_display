namespace HurricaneBeastPanel;

/// <summary>
/// A pretend screen for development: no hardware needed. It is only found when the environment variable HB_SIMULATOR=1
/// is set (or the Driver setting is "Simulator"). Every picture the app would send is saved as
/// %AppData%\HurricaneBeastPanel\simulator.png, so you can build and test a theme, or a new driver's plumbing, without a screen.
/// </summary>
public sealed class SimulatorFactory : IScreenDriverFactory
{
    public const string Port = "SIMULATOR";

    public string Brand => "Simulator";
    public string? FindPort() => Environment.GetEnvironmentVariable("HB_SIMULATOR") == "1" ? Port : null;
    public bool PortExists(string port) => port == Port;
    public IScreenDriver Create() => new SimulatorDriver();
}

public sealed class SimulatorDriver : IScreenDriver
{
    public static string OutputPath => Path.Combine(AppSettings.Dir, "simulator.png");

    long _lastSave;
    int _brightness = 100;

    public ScreenInfo Open(string port) =>
        new("Simulator", "1.0", 1920, 462, SupportsBrightness: true, SupportsRestart: true, DimInSoftware: false);

    public void BeginStreaming(int brightness) => _brightness = brightness;

    public void SetFrame(Bitmap frame, bool flip180)
    {
        if (Environment.TickCount64 - _lastSave < 500) return; // do not write a file for every frame
        _lastSave = Environment.TickCount64;
        try
        {
            Directory.CreateDirectory(AppSettings.Dir);
            if (flip180) frame.RotateFlip(RotateFlipType.Rotate180FlipNone);
            frame.Save(OutputPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { /* the file may be open in a viewer */ }
    }

    public void Send() { }

    public void SetBrightness(int percent) => _brightness = percent;

    public void Restart() { }

    public void Dispose() { }
}
