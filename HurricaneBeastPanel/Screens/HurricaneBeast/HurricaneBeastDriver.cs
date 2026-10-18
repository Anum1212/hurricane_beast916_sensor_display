using System.Globalization;

namespace HurricaneBeastPanel;

/// <summary>Finds Hurricane Beast screens (USB 33C3:7788, shown as a COM port).</summary>
public sealed class HurricaneBeastFactory : IScreenDriverFactory
{
    public string Brand => "Hurricane Beast";
    public string? FindPort() => ScreenDevice.FindPort();
    public bool PortExists(string port) => ScreenDevice.PortExists(port);
    public IScreenDriver Create() => new HurricaneBeastDriver();
}

/// <summary>
/// Hurricane Beast screens: a serial link where the screen shows bare JPEG frames once live mode is on.
/// The wire protocol itself is in <see cref="ScreenDevice"/>; this class adapts it to <see cref="IScreenDriver"/>.
/// </summary>
public sealed class HurricaneBeastDriver : IScreenDriver
{
    const int KeepAliveMs = 1500;

    readonly ScreenDevice _device = new();
    readonly byte[] _keepAlive = ScreenDevice.Build(ScreenDevice.CmdLive);
    DeviceReport? _report;
    byte[]? _jpeg;
    int _quality = 90;
    long _lastKeepAlive;

    public ScreenInfo Open(string port)
    {
        var report = _device.Open(port);
        _report = report;
        return new ScreenInfo(
            report.Model, report.Version.ToString("0.0", CultureInfo.InvariantCulture), report.Width, report.Height,
            SupportsBrightness: report.SupportsBrightness, SupportsRestart: true,
            // While pictures stream the screen does not visibly change its backlight, so the picture is darkened too
            // (the vendor app does the same).
            DimInSoftware: true);
    }

    public void BeginStreaming(int brightness)
    {
        if (_report!.SupportsBrightness) _device.SetBrightness(brightness);
        _device.SendCommand(ScreenDevice.CmdLive);
        _lastKeepAlive = Environment.TickCount64;
    }

    public void SetFrame(Bitmap frame, bool flip180)
    {
        var report = _report!;
        // The panel wants its own orientation: a wide bar takes portrait frames, so a horizontal theme is turned
        // 90 degrees (270 clockwise); the flip option reverses that.
        bool sameShape = frame.Width == report.FrameWidth && frame.Height == report.FrameHeight;
        var turn = sameShape
            ? (flip180 ? RotateFlipType.Rotate180FlipNone : RotateFlipType.RotateNoneFlipNone)
            : (flip180 ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate270FlipNone);
        if (turn != RotateFlipType.RotateNoneFlipNone) frame.RotateFlip(turn);
        _jpeg = FrameTools.EncodeJpeg(frame, report.MaxJpegKb * 1024, ref _quality);
    }

    public void Send()
    {
        if (_jpeg != null) _device.WriteRaw(_jpeg);
        if (Environment.TickCount64 - _lastKeepAlive >= KeepAliveMs)
        {
            _lastKeepAlive = Environment.TickCount64;
            _device.WriteRaw(_keepAlive);
        }
    }

    public void SetBrightness(int percent) => _device.SetBrightness(percent);

    public void Restart() => _device.SendCommand(ScreenDevice.CmdRestart);

    public void Dispose() => _device.Dispose();
}
