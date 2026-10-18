namespace HurricaneBeastPanel;

// =====================================================================================================================
// Screen drivers
//
// Everything in the app that is specific to one brand of screen lives behind these two interfaces. The sensors, the
// theme renderer, the editor and the streaming loop (PanelEngine) only talk to them. To support another screen,
// write a class that implements IScreenDriver, a factory that implements IScreenDriverFactory, and add the factory to
// ScreenDrivers.All. See docs/ADDING_A_SCREEN.md for a walk-through.
// =====================================================================================================================

/// <summary>What the app needs to know about a connected screen.</summary>
/// <param name="Model">Name shown in the app, e.g. "D215-FL7707N-9.16inch-hor".</param>
/// <param name="Firmware">Firmware or version text, "-" when the screen does not report one.</param>
/// <param name="Width">Width of the picture the screen shows, as the user looks at it (1920 for a wide bar).</param>
/// <param name="Height">Height of that picture (462 for a wide bar).</param>
/// <param name="SupportsBrightness">The screen has a brightness command the driver can use.</param>
/// <param name="SupportsRestart">The screen can be restarted from the app.</param>
/// <param name="DimInSoftware">
/// The app also darkens the picture itself when brightness is lowered (needed when the backlight command does not
/// visibly work while pictures are streaming). Leave false when <see cref="SupportsBrightness"/> really dims the screen.
/// </param>
public sealed record ScreenInfo(
    string Model, string Firmware, int Width, int Height,
    bool SupportsBrightness = false, bool SupportsRestart = false, bool DimInSoftware = false);

/// <summary>A connection to one screen. Created by an <see cref="IScreenDriverFactory"/>; the engine calls it from one thread.</summary>
public interface IScreenDriver : IDisposable
{
    /// <summary>Opens the connection and returns facts about the screen. Throws when the screen does not answer.</summary>
    ScreenInfo Open(string port);

    /// <summary>
    /// Called once after <see cref="Open"/> and before the first picture: switch the screen into the mode that shows
    /// pictures and apply the starting brightness (0-100).
    /// </summary>
    void BeginStreaming(int brightness);

    /// <summary>
    /// A new picture has been drawn (at most once a second for a static theme, every frame for animated ones).
    /// The bitmap is <see cref="ScreenInfo.Width"/> x <see cref="ScreenInfo.Height"/>, or the two swapped when the theme
    /// is vertical. Convert it into whatever the screen needs (rotate, encode, change pixel format) and keep the result.
    /// The bitmap belongs to the driver for the duration of the call and may be modified; do not keep a reference to it.
    /// </summary>
    /// <param name="flip180">The user turned on "Flip image upside-down".</param>
    void SetFrame(Bitmap frame, bool flip180);

    /// <summary>
    /// Called every 1/fps of a second while streaming: write the latest picture to the screen, and anything else the
    /// screen needs now and then (for example a keep-alive message). Do nothing if the screen only wants changes.
    /// </summary>
    void Send();

    /// <summary>Changes the brightness (0-100). Only called when <see cref="ScreenInfo.SupportsBrightness"/> is true.</summary>
    void SetBrightness(int percent);

    /// <summary>Restarts the screen. Only called when <see cref="ScreenInfo.SupportsRestart"/> is true.</summary>
    void Restart();
}

/// <summary>Knows how to find and create drivers for one brand of screen.</summary>
public interface IScreenDriverFactory
{
    /// <summary>Brand name, e.g. "Hurricane Beast". Also the value of the Driver setting that limits detection to this brand.</summary>
    string Brand { get; }

    /// <summary>Looks for a connected screen of this brand and returns the port to open (e.g. "COM5"), or null if there is none.</summary>
    string? FindPort();

    /// <summary>True if the named port exists right now (used when the user picked a port by hand).</summary>
    bool PortExists(string port);

    IScreenDriver Create();
}

/// <summary>The screens this build supports. Add a factory here to support another brand.</summary>
public static class ScreenDrivers
{
    public static readonly IReadOnlyList<IScreenDriverFactory> All = new IScreenDriverFactory[]
    {
        new HurricaneBeastFactory(),
        new SimulatorFactory(), // pretend screen for development, only active with HB_SIMULATOR=1
    };

    /// <summary>
    /// Picks the screen to connect to: the port chosen in the settings, or the first screen any driver can find.
    /// The Driver setting, when not empty, limits the search to that brand.
    /// </summary>
    public static (IScreenDriverFactory factory, string port)? Locate(AppSettings settings)
    {
        var candidates = All
            .Where(f => string.IsNullOrWhiteSpace(settings.Driver) || f.Brand.Equals(settings.Driver, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrWhiteSpace(settings.Port))
        {
            foreach (var f in candidates)
                if (f.PortExists(settings.Port)) return (f, settings.Port);
            return null;
        }
        foreach (var f in candidates)
        {
            string? port = f.FindPort();
            if (port != null) return (f, port);
        }
        return null;
    }
}
