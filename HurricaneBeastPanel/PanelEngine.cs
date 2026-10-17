using System.Diagnostics;
using System.Drawing.Imaging;
using Microsoft.Win32;

namespace HurricaneBeastPanel;

/// <summary>Background loop: finds the screen, connects, renders the dashboard and streams JPEG frames. Reconnects on its own.</summary>
public sealed class PanelEngine : IDisposable
{
    const int RenderIntervalMs = 1000;
    const int KeepAliveMs = 1500;

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

    /// <summary>
    /// Rotates the rendered theme into the orientation of the frame the panel expects.
    /// A horizontal theme on a panel that wants portrait frames is turned 90 degrees (270 clockwise); the flip option reverses that.
    /// </summary>
    static void Orient(Bitmap bmp, ScreenInfo info, bool flip, int logicalW, int logicalH)
    {
        bool sameShape = logicalW == info.FrameWidth && logicalH == info.FrameHeight;
        var rf = sameShape
            ? (flip ? RotateFlipType.Rotate180FlipNone : RotateFlipType.RotateNoneFlipNone)
            : (flip ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate270FlipNone);
        if (rf != RotateFlipType.RotateNoneFlipNone) bmp.RotateFlip(rf);
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
    /// Restarts the screen with its own restart command (the same one the vendor app uses).
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
                string? port = string.IsNullOrWhiteSpace(_settings.Port) ? ScreenDevice.FindPort() : _settings.Port;
                if (port == null) { Status("Screen not found - plug it in"); return; }
                using var dev = new ScreenDevice();
                dev.Open(port);
                dev.SendCommand(ScreenDevice.CmdRestart);
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
            ScreenDevice? dev = null;
            try
            {
                string? port = string.IsNullOrWhiteSpace(_settings.Port)
                    ? ScreenDevice.FindPort()
                    : ScreenDevice.PortExists(_settings.Port) ? _settings.Port : null;
                if (port == null)
                {
                    Once("Screen not found - plug it in");
                    await Task.Delay(2000, ct);
                    continue;
                }

                Once($"Connecting on {port}...");
                dev = new ScreenDevice();
                var info = dev.Open(port);
                DeviceChanged?.Invoke(info);
                if (info.SupportsBrightness) dev.SetBrightness(_settings.Brightness);
                dev.SendCommand(ScreenDevice.CmdLive);
                Once($"Streaming to {info.Model} on {port}");
                lastStatus = "";

                await StreamAsync(dev, info, ct);
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
                dev?.Dispose();
                DeviceChanged?.Invoke(null);
            }

            try { await Task.Delay(2000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// The screen does not visibly change its backlight while a picture is streaming, so the picture itself is darkened too
    /// (a black layer, the same way the vendor app does it). The preview is taken before this, so it stays bright.
    /// </summary>
    static void DimFrame(Bitmap frame, int brightness)
    {
        int alpha = (int)Math.Round((100 - Math.Clamp(brightness, 0, 100)) / 100.0 * 255);
        if (alpha <= 0) return;
        using var g = Graphics.FromImage(frame);
        using var black = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
        g.FillRectangle(black, 0, 0, frame.Width, frame.Height);
    }

    async Task StreamAsync(ScreenDevice dev, ScreenInfo info, CancellationToken ct)
    {
        byte[]? jpeg = null;
        var sw = Stopwatch.StartNew();
        long lastRender = long.MinValue / 2, lastKeepAlive = 0, lastPreview = 0;
        byte[] keepAlive = ScreenDevice.Build(ScreenDevice.CmdLive);
        Theme? lastTheme = null;
        bool animated = false;
        int quality = 90;
        _reconnect = false;

        while (!ct.IsCancellationRequested)
        {
            if (_reconnect) throw new IOException("Reconnecting after resume");

            if (_restartRequested)
            {
                _restartRequested = false;
                dev.SendCommand(ScreenDevice.CmdRestart);
                await Task.Delay(300, ct);
                throw new ScreenRestartingException();
            }

            if (_brightnessDirty)
            {
                _brightnessDirty = false;
                if (info.SupportsBrightness) dev.SetBrightness(_settings.Brightness);
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
                // Draw the theme in its own orientation, then turn it into the frame the panel expects.
                int shortSide = Math.Min(info.Width, info.Height), longSide = Math.Max(info.Width, info.Height);
                int lw = theme.IsVertical ? shortSide : longSide;
                int lh = theme.IsVertical ? longSide : shortSide;
                using var frame = ThemeRenderer.Render(theme, snapshot, _settings, lw, lh);
                if (sw.ElapsedMilliseconds - lastPreview >= 250)
                {
                    lastPreview = sw.ElapsedMilliseconds;
                    FrameReady?.Invoke((Bitmap)frame.Clone());
                }
                DimFrame(frame, _settings.Brightness);
                Orient(frame, info, _settings.Flip180, lw, lh);
                jpeg = EncodeJpeg(frame, info.MaxJpegKb * 1024, ref quality);
            }

            if (jpeg != null) dev.WriteRaw(jpeg);

            if (sw.ElapsedMilliseconds - lastKeepAlive >= KeepAliveMs)
            {
                lastKeepAlive = sw.ElapsedMilliseconds;
                dev.WriteRaw(keepAlive);
            }

            await Task.Delay(sendInterval, ct);
        }
    }

    static readonly ImageCodecInfo JpegCodec =
        ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");

    /// <summary>Encodes as JPEG, lowering quality until the frame fits the firmware's size limit.</summary>
    public static byte[] EncodeJpeg(Bitmap bmp, int maxBytes)
    {
        int quality = 90;
        return EncodeJpeg(bmp, maxBytes, ref quality);
    }

    /// <summary>
    /// Same, but starts from the quality that worked last time so animated themes do not re-encode several times per frame.
    /// The quality creeps back up when frames come in well under the limit.
    /// </summary>
    public static byte[] EncodeJpeg(Bitmap bmp, int maxBytes, ref int quality)
    {
        for (int q = Math.Clamp(quality, 20, 92); ; q -= 5)
        {
            using var parms = new EncoderParameters(1);
            parms.Param[0] = new EncoderParameter(Encoder.Quality, (long)q);
            using var ms = new MemoryStream();
            bmp.Save(ms, JpegCodec, parms);
            if (ms.Length <= maxBytes || q <= 20)
            {
                quality = ms.Length < maxBytes * 0.6 ? Math.Min(q + 2, 92) : q;
                return ms.ToArray();
            }
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        StopAsync().GetAwaiter().GetResult();
        _sensors.Dispose();
    }
}
