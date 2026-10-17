# Hurricane Beast 9.16" Sensor Display

A Windows app that replaces HT PLAYER for Hurricane Beast USB sensor screens (USB `33C3:7788`, shown as a COM port). It streams a live sensor dashboard to the screen and includes a theme editor.

## Features
- Live CPU, GPU, RAM, VRAM, fan, network and drive sensors (LibreHardwareMonitor).
- Theme editor: text, shapes, gradients, glow, charts, gauges, graphs, meters, date/time formats, images, GIFs and MP4 (also as backgrounds).
- Horizontal (1920x462) and vertical (462x1920) themes.
- Built-in themes: Default, Neon Dashboard, Tile, Cards, Compact, Smoky (horizontal and vertical variants).
- Custom fonts: drop `.ttf` or `.otf` files into `%AppData%\HurricaneBeastPanel\fonts`.
- Tray app, autostart, brightness, and a Restart screen button.

## Build
Requires the .NET 10 SDK on Windows.

```
cd HurricaneBeastPanel
dotnet build -c Release -o bin\dev
```

CPU temperature and power need administrator rights, so the app asks for elevation on start.

## Protocol notes
Packet: `55 AA len(2) cmd payload sum(2)`. Commands: `0x06` info, `0x11` frame (JPEG, 462x1920) and keepalive, `0x03` brightness, `0x01` restart. DTR must be enabled on the serial port.
