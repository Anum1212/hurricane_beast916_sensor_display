# Adding support for another screen

The app is built so that everything specific to one brand of screen sits behind two small interfaces in
[`HurricaneBeastPanel/Screens/IScreenDriver.cs`](../HurricaneBeastPanel/Screens/IScreenDriver.cs). The sensors, the theme
renderer, the editor and the streaming loop (`PanelEngine`) do not know which screen they are talking to.

```
sensors -> theme renderer -> picture -> PanelEngine -> IScreenDriver -> your screen
```

If you add support for a new brand, please give credit to this project in your README (see the main README). Thank you.

## What you write

1. **A driver** (`IScreenDriver`): opens the connection and turns a picture into what the screen wants.
2. **A factory** (`IScreenDriverFactory`): finds the screen (for example by USB ID) and creates the driver.
3. **One line** in `ScreenDrivers.All` to register the factory.

Two complete examples are in the repo:

| Example | What it shows |
|---|---|
| [`Screens/HurricaneBeast/`](../HurricaneBeastPanel/Screens/HurricaneBeast) | A real screen: USB serial, packet protocol, JPEG frames, brightness, restart, keep-alive. |
| [`Screens/Simulator/`](../HurricaneBeastPanel/Screens/Simulator) | A pretend screen that saves each picture to a PNG. The smallest possible driver, and a handy way to test without hardware. |

## The interfaces

```csharp
public interface IScreenDriverFactory
{
    string Brand { get; }                  // "My Brand"; also the value of the Driver setting
    string? FindPort();                    // e.g. "COM7" if a screen of this brand is connected, else null
    bool PortExists(string port);          // used when the user picked a port by hand
    IScreenDriver Create();
}

public interface IScreenDriver : IDisposable
{
    ScreenInfo Open(string port);                    // connect; describe the screen
    void BeginStreaming(int brightness);             // switch to picture mode, apply brightness
    void SetFrame(Bitmap frame, bool flip180);       // a new picture was drawn: convert and keep it
    void Send();                                     // every 1/fps second: write the latest picture
    void SetBrightness(int percent);                 // only if ScreenInfo.SupportsBrightness
    void Restart();                                  // only if ScreenInfo.SupportsRestart
}
```

`ScreenInfo` tells the app the screen's model, firmware text, the size of the picture it shows (`Width` x `Height`, as the
user looks at it) and what it can do (`SupportsBrightness`, `SupportsRestart`, `DimInSoftware`).

### How a frame travels

1. The engine draws the theme at `Width` x `Height` (swapped for a vertical theme) and shows it in the app's preview.
2. If `DimInSoftware` is true it darkens the picture for the current brightness.
3. It calls `driver.SetFrame(bitmap, flip180)`. This is where you rotate, change the pixel format (for example
   `FrameTools.ToRgb565`), compress (`FrameTools.EncodeJpeg`) and store the bytes.
4. It calls `driver.Send()` every `1/fps` second. Write the stored bytes (and any keep-alive) to the screen.

Static themes produce a new picture about once a second. Animated ones (GIF, video, weather) produce one every frame.
`Send()` is called at the chosen frame rate either way, so it may be called many times for the same picture.

## A minimal driver

```csharp
public sealed class MyBrandFactory : IScreenDriverFactory
{
    public string Brand => "My Brand";
    public string? FindPort() => /* look up your USB VID/PID, return "COMx" or null */ null;
    public bool PortExists(string port) => System.IO.Ports.SerialPort.GetPortNames().Contains(port);
    public IScreenDriver Create() => new MyBrandDriver();
}

public sealed class MyBrandDriver : IScreenDriver
{
    System.IO.Ports.SerialPort? _port;
    byte[]? _pixels;

    public ScreenInfo Open(string port)
    {
        _port = new System.IO.Ports.SerialPort(port, 115200) { DtrEnable = true };
        _port.Open();
        return new ScreenInfo("My Screen 5\"", "1.0", 800, 480, SupportsBrightness: true);
    }

    public void BeginStreaming(int brightness) => SetBrightness(brightness);

    public void SetFrame(Bitmap frame, bool flip180)
    {
        if (flip180) frame.RotateFlip(RotateFlipType.Rotate180FlipNone);
        _pixels = FrameTools.ToRgb565(frame);
    }

    public void Send() { if (_pixels != null) _port!.Write(_pixels, 0, _pixels.Length); }
    public void SetBrightness(int percent) { /* your command */ }
    public void Restart() { }
    public void Dispose() => _port?.Dispose();
}
```

Then register it:

```csharp
public static readonly IReadOnlyList<IScreenDriverFactory> All = new IScreenDriverFactory[]
{
    new HurricaneBeastFactory(),
    new MyBrandFactory(),
    new SimulatorFactory(),
};
```

## Rules of thumb

- **Throw from `Open`** if the screen does not answer. The app then shows "Connection lost" and retries every two seconds.
- **Never block for ever.** Serial ports that are unplugged or rebooting can hang on `Close()`; do the closing on another
  thread with a time limit (see `ScreenDevice.Dispose` for how this project does it).
- **Do not keep the bitmap** passed to `SetFrame`; the engine disposes it afterwards. Copy what you need.
- **Keep `Send()` fast.** It runs on the streaming loop; a slow write lowers the real frame rate.
- **Brightness.** If the backlight command does not visibly work while pictures stream, set `DimInSoftware: true` and the
  app darkens the picture for you.
- **Several brands at once.** `ScreenDrivers.Locate` tries each factory in order. The `Driver` setting (in `settings.json`)
  limits detection to one brand.

## Testing without a screen

Set the environment variable `HB_SIMULATOR=1` and start the app. The Simulator driver is then found like a real screen and
saves every picture it would send to `%AppData%\HurricaneBeastPanel\simulator.png`. `HurricaneBeastPanel.exe --engine-test
result.txt` runs the complete streaming loop (connect, stream, restart, reconnect) against it and writes a short report.

## Finding out a screen's protocol

Vendor apps often ship as Electron or .NET programs that can be read directly. For Hurricane Beast the packet layout, the
commands and the frame size limits came from reading the vendor app's code, then confirming them with a small test program
on the real screen. Check that doing so is allowed for the software you are looking at.
