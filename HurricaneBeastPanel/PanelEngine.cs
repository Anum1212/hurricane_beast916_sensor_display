using System.Diagnostics;
using Microsoft.Win32;

namespace HurricaneBeastPanel;

/// <summary>
/// Background loop: finds a screen through the screen drivers, connects, renders the dashboard and streams it.
/// Reconnects on its own. Everything specific to one brand of screen is in its <see cref="IScreenDriver"/>.
/// </summary>
public sealed class PanelEngine : IDisposable
{
    const int RenderIntervalMs = 1000;

    readonly AppSettings _settings;
    readonly SensorService _sensors = new();
    readonly object _snapLock = new();
    SensorSnapshot? _lastSnapshot;
    long _lastSnapshotTick;
    volatile Theme _theme;
    CancellationTokenSource? _cts;
    Task? _task;
    volatile bool _brightnessDirty;
    volatile bool _restartRequested;
    volatile bool _reconnect;

    public event Action<string>? StatusChanged;
    public event Action<Bitmap>? FrameReady;
    public event Action<ScreenInfo?>? DeviceChanged;

    public bool Running => _task is { IsCompleted: false };
    public string? SensorError => _sensors.OpenError;

    public PanelEngine(AppSettings settings)
    {
        _settings = settings;
        _theme = ThemeStore.Load(settings.ThemeName);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    /// <summary>Swaps the theme being streamed. The theme must not be modified afterwards (pass a clone).</summary>
    public void SetTheme(Theme theme)
    {
        MediaCache.Prune(theme.MediaPaths());
        _theme = theme;
    }

    /// <summary>Latest sensor readings, polled at most about once per second.</summary>
    public SensorSnapshot GetSnapshot()
    {
        lock (_snapLock)
        {
            if (_lastSnapshot == null || Environment.TickCount64 - _lastSnapshotTick > 900)
            {
                _lastSnapshot = _sensors.Poll();
                _lastSnapshotTick = Environment.TickCount64;
                SensorHistory.Push(_lastSnapshot);
            }
            return _lastSnapshot;
        }
    }

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        // After sleep the USB port is usually gone; drop the connection and re-open it.
        if (e.Mode == PowerModes.Resume) _reconnect = true;
    }

    public void Start()
    {
        if (Running) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _task = Task.Run(() => RunAsync(ct));
    }

    public async Task StopAsync()
    {
        var cts = _cts;
        var task = _task;
        if (cts == null || task == null) return;
        cts.Cancel();
        try { await task.ConfigureAwait(false); } catch (OperationCanceledException) { }
        cts.Dispose();
        _cts = null;
        _task = null;
        Status("Stopped");
    }

    public void ApplyBrightness() => _brightnessDirty = true;

    sealed class ScreenRestartingException : Exception { }

    /// <summary>
    /// Restarts the screen with its own restart command.
    /// While streaming the command goes out on the open connection and the app reconnects when the screen is back;
    /// when stopped it briefly opens the port just for this.
    /// </summary>
    public void RestartScreen()
    {
        if (Running)
        {
            _restartRequested = true;
            return;
        }
        Task.Run(() =>
        {
            try
            {
                var found = ScreenDrivers.Locate(_settings);
                if (found == null) { Status("Screen not found - plug it in"); return; }
                using var driver = found.Value.factory.Create();
                var info = driver.Open(found.Value.port);
                if (!info.SupportsRestart) { Status("This screen cannot be restarted from the app"); return; }
                driver.Restart();
                Status("Screen restarting...");
            }
            catch (UnauthorizedAccessException) { Status("COM port is busy - stop streaming first"); }
            catch (Exception ex) { Status("Could not restart the screen: " + ex.Message); }
        });
    }

    void Status(string text)
    {
        Log.Write(text);
        StatusChanged?.Invoke(text);
    }

    async Task RunAsync(CancellationToken ct)
    {
        string lastStatus = "";
        void Once(string text)
        {
            if (text == lastStatus) return;
            lastStatus = text;
            Status(text);
        }

        while (!ct.IsCancellationRequested)
        {
            IScreenDriver? driver = null;
            try
            {
                var found = ScreenDrivers.Locate(_settings);
                if (found == null)
                {
                    Once("Screen not found - plug it in");
                    await Task.Delay(2000, ct);
                    continue;
                }
                string port = found.Value.port;

                Once($"Connecting on {port}...");
                driver = found.Value.factory.Create();
                var info = driver.Open(port);
                DeviceChanged?.Invoke(info);
                driver.BeginStreaming(_settings.Brightness);
                Once($"Streaming to {info.Model} on {port}");
                lastStatus = "";

                await StreamAsync(driver, info, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ScreenRestartingException)
            {
                Once("Screen restarting...");
                // The screen reboots and shows up on USB again after a few seconds.
                try { await Task.Delay(3500, ct); } catch (OperationCanceledException) { break; }
            }
            catch (UnauthorizedAccessException)
            {
                Once("COM port is busy - close HT PLAYER and re-plug the screen");
            }
            catch (Exception ex)
            {
                Once("Connection lost: " + ex.Message);
            }
            finally
            {
                driver?.Dispose();
                DeviceChanged?.Invoke(null);
            }

            try { await Task.Delay(2000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    async Task StreamAsync(IScreenDriver driver, ScreenInfo info, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        long lastRender = long.MinValue / 2, lastPreview = 0;
        Theme? lastTheme = null;
        bool animated = false;
        _reconnect = false;

        while (!ct.IsCancellationRequested)
        {
            if (_reconnect) throw new IOException("Reconnecting after resume");

            if (_restartRequested)
            {
                _restartRequested = false;
                if (info.SupportsRestart)
                {
                    driver.Restart();
                    await Task.Delay(300, ct);
                    throw new ScreenRestartingException();
                }
                Status("This screen cannot be restarted from the app");
            }

            if (_brightnessDirty)
            {
                _brightnessDirty = false;
                if (info.SupportsBrightness) driver.SetBrightness(_settings.Brightness);
                lastRender = long.MinValue / 2; // redraw now so the new brightness shows at once
            }

            int sendInterval = 1000 / Math.Clamp(_settings.SendFps, 1, 30);
            var theme = _theme;
            if (!ReferenceEquals(theme, lastTheme))
            {
                lastTheme = theme;
                animated = ThemeRenderer.HasAnimation(theme);
                lastRender = long.MinValue / 2; // draw the new theme immediately
            }

            // Sensor values change about once a second; a GIF or video needs a new picture every frame.
            long renderEvery = animated ? sendInterval : RenderIntervalMs;
            if (sw.ElapsedMilliseconds - lastRender >= renderEvery)
            {
                lastRender = sw.ElapsedMilliseconds;
                var snapshot = GetSnapshot();
                // Draw the theme in its own orientation; the driver turns it into the frame the screen expects.
                int shortSide = Math.Min(info.Width, info.Height), longSide = Math.Max(info.Width, info.Height);
                int lw = theme.IsVertical ? shortSide : longSide;
                int lh = theme.IsVertical ? longSide : shortSide;
                using var frame = ThemeRenderer.Render(theme, snapshot, _settings, lw, lh);
                if (sw.ElapsedMilliseconds - lastPreview >= 250)
                {
                    lastPreview = sw.ElapsedMilliseconds;
                    FrameReady?.Invoke((Bitmap)frame.Clone());
                }
                if (info.DimInSoftware) FrameTools.Dim(frame, _settings.Brightness); // the preview above stays bright
                driver.SetFrame(frame, _settings.Flip180);
            }

            driver.Send();

            await Task.Delay(sendInterval, ct);
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        StopAsync().GetAwaiter().GetResult();
        _sensors.Dispose();
    }
}
