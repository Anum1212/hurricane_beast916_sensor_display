using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace HurricaneBeastPanel;

/// <summary>Device description returned by command 0x06.</summary>
public sealed record ScreenInfo(
    string Uid, int Width, int Height, int Angle, int Brightness, string Model, double Version)
{
    // The vendor app swaps the axes for 90/270 degree panels, so frames are portrait on a 1920x462 bar.
    public int FrameWidth => Angle is 90 or 270 ? Height : Width;
    public int FrameHeight => Angle is 90 or 270 ? Width : Height;

    /// <summary>Largest JPEG the firmware accepts, in KB (limits taken from the vendor app).</summary>
    public int MaxJpegKb
    {
        get
        {
            if (Model.Contains("6.67")) return 300;
            bool newFw = Version > 2.8;
            return Model.Contains("9.16") ? (newFw ? 120 : 90) : (newFw ? 80 : 50);
        }
    }

    public bool SupportsBrightness => Version >= 2.3;
}

/// <summary>
/// USB CDC serial screen (VID 33C3, PID 7788).
/// Packet: 55 AA lenLo lenHi cmd payload sumLo sumHi, where len = payload + 7 and sum = byte sum of everything before it.
/// In live mode (0x11) the screen shows bare JPEG frames written to the port.
/// </summary>
public sealed class ScreenDevice : IDisposable
{
    public const byte CmdRestart = 0x01;
    public const byte CmdBrightness = 0x03;
    public const byte CmdInfo = 0x06;
    public const byte CmdLive = 0x11;

    const string UsbId = "VID_33C3&PID_7788";

    readonly object _writeLock = new();
    readonly List<byte> _rx = new();
    SerialPort? _port;

    public static byte[] Build(byte cmd, params byte[] payload)
    {
        int len = payload.Length + 7;
        var p = new List<byte> { 0x55, 0xAA, (byte)(len & 0xFF), (byte)(len >> 8), cmd };
        p.AddRange(payload);
        int sum = 0;
        foreach (byte b in p) sum += b;
        p.Add((byte)(sum & 0xFF));
        p.Add((byte)((sum >> 8) & 0xFF));
        return p.ToArray();
    }

    /// <summary>Finds the COM port of a connected screen via the USB enumeration registry.</summary>
    public static string? FindPort()
    {
        var present = new HashSet<string>(SerialPort.GetPortNames(), StringComparer.OrdinalIgnoreCase);
        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
        if (usb == null) return null;
        foreach (string dev in usb.GetSubKeyNames())
        {
            if (!dev.StartsWith(UsbId, StringComparison.OrdinalIgnoreCase)) continue;
            using var devKey = usb.OpenSubKey(dev);
            if (devKey == null) continue;
            foreach (string inst in devKey.GetSubKeyNames())
            {
                using var parms = devKey.OpenSubKey(inst + @"\Device Parameters");
                if (parms?.GetValue("PortName") is string name && present.Contains(name))
                    return name;
            }
        }
        return null;
    }

    public static bool PortExists(string name) =>
        SerialPort.GetPortNames().Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Opens the port and reads the device description. Throws if the screen does not answer.</summary>
    public ScreenInfo Open(string portName)
    {
        // Baud rate is ignored by USB CDC, DTR must be asserted.
        var sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1000,
            WriteTimeout = 2000,
            DtrEnable = true,
            RtsEnable = true,
        };
        sp.DataReceived += OnData;
        sp.Open();
        _port = sp;
        // Give the screen a moment after the port opens (DTR goes high), and drop anything left over from the last session.
        Thread.Sleep(250);
        try { sp.DiscardInBuffer(); } catch { }
        lock (_rx) _rx.Clear();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            var info = QueryInfo();
            if (info != null) return info;
        }
        throw new IOException("Screen did not answer the info request");
    }

    void OnData(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var sp = (SerialPort)sender;
            int n = sp.BytesToRead;
            if (n <= 0) return;
            var buf = new byte[n];
            int read = sp.Read(buf, 0, n);
            lock (_rx)
            {
                _rx.AddRange(buf.AsSpan(0, read).ToArray());
                if (_rx.Count > 65536) _rx.Clear();
            }
        }
        catch { /* port closing */ }
    }

    ScreenInfo? QueryInfo()
    {
        lock (_rx) _rx.Clear();
        WriteRaw(Build(CmdInfo));
        var deadline = DateTime.UtcNow.AddMilliseconds(2000);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
            byte[] data;
            lock (_rx) data = _rx.ToArray();
            var info = TryParseInfo(data);
            if (info != null) return info;
        }
        return null;
    }

    static ScreenInfo? TryParseInfo(byte[] data)
    {
        int start = Array.IndexOf(data, (byte)'{');
        int end = Array.LastIndexOf(data, (byte)'}');
        if (start < 0 || end <= start) return null;
        try
        {
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(data, start, end - start + 1));
            if (!doc.RootElement.TryGetProperty("data", out var d)) return null;
            int Int(string k, int def = 0) => d.TryGetProperty(k, out var v) && v.TryGetInt32(out int i) ? i : def;
            string Str(string k) => d.TryGetProperty(k, out var v) ? v.ToString() : "";
            double.TryParse(Str("version"), NumberStyles.Float, CultureInfo.InvariantCulture, out double ver);
            return new ScreenInfo(Str("uid"), Int("width"), Int("height"), Int("angle"),
                Int("brightness", 100), Str("model"), ver);
        }
        catch (JsonException) { return null; }
    }

    public void SendCommand(byte cmd, params byte[] payload) => WriteRaw(Build(cmd, payload));

    public void SetBrightness(int percent) =>
        SendCommand(CmdBrightness, (byte)Math.Clamp(percent, 0, 100));

    public void WriteRaw(byte[] data)
    {
        var sp = _port ?? throw new InvalidOperationException("Port is not open");
        lock (_writeLock) sp.Write(data, 0, data.Length);
    }

    public void Dispose()
    {
        var sp = _port;
        _port = null;
        if (sp == null) return;
        try { sp.DataReceived -= OnData; } catch { }
        // Closing a USB serial port can block for ever when the screen has just rebooted or been unplugged, which froze the
        // whole connection loop ("screen not detected" until the app was killed). Do it on another thread and stop waiting.
        var closer = Task.Run(() =>
        {
            try
            {
                if (sp.IsOpen)
                {
                    // Let the frame that is being sent finish so the screen is never left with half a picture.
                    long end = Environment.TickCount64 + 1500;
                    while (sp.BytesToWrite > 0 && Environment.TickCount64 < end) Thread.Sleep(10);
                    Thread.Sleep(80);
                    sp.Close();
                }
            }
            catch { }
            try { sp.Dispose(); } catch { }
        });
        if (!closer.Wait(4000)) Log.Write("Closing the serial port took too long; continuing without waiting");
    }
}
