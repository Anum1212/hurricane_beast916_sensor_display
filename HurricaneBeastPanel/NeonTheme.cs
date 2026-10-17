namespace HurricaneBeastPanel;

/// <summary>
/// "Neon Dashboard": a dark blue panel with glowing edges, gradient rings and italic text, laid out for the 1920x462 screen.
/// Built only from editor elements, so everything here can be changed in the theme editor.
/// </summary>
public static class NeonTheme
{
    const string Font = "Bahnschrift";
    const string Blue = "#FF2F6BFF", Violet = "#FF2A2AFF", Magenta = "#FFD21CFF", Pink = "#FFE23CFF";
    const string Label = "#FF8E9BFF", White = "#FFFFFFFF", Dark = "#FF16165E";

    static FillSpec Ring() => new() { Type = FillType.Linear, Color1 = Violet, Color2 = Magenta, Color3 = Magenta, ColorCount = 2, Angle = 0 };
    static FillSpec Title() => new() { Type = FillType.Linear, Color1 = "#FFA06CFF", Color2 = Pink, ColorCount = 2, Angle = 0 };
    static FillSpec Bar() => new() { Type = FillType.Linear, Color1 = "#FF7A1CFF", Color2 = Magenta, ColorCount = 2, Angle = 0 };

    static Element Text(string name, string text, float x, float y, float w, float h, float size, string color,
        HAlign align = HAlign.Left, bool bold = false, FillSpec? fill = null, float glow = 0, string glowColor = Magenta)
    {
        var e = Theme.TextEl(name, text, x, y, w, h, size, bold, color, align);
        e.FontFamily = Font;
        e.Italic = true;
        if (fill != null) e.Fill = fill;
        e.GlowSize = glow;
        e.GlowColor = glowColor;
        return e;
    }

    static Element Gauge(string name, string bind, float x, float y, float size, float max = 100)
    {
        var e = Element.CreateWidget(WidgetStyle.ThreeQuarterArc, 1, size, size);
        e.Name = name; e.X = x; e.Y = y; e.Width = size; e.Height = size;
        e.Bind = bind; e.BindMin = 0; e.BindMax = max;
        e.Fill = Ring();
        e.RemainFill = FillSpec.Solid(Dark);
        e.RingThickness = size * 0.105f;
        return e;
    }

    static Element Bars(string name, string bind, float x, float y, float w, float h)
    {
        var e = Element.CreateWidget(WidgetStyle.SignalStrength, 1, w, h);
        e.Name = name; e.X = x; e.Y = y; e.Width = w; e.Height = h;
        e.Bind = bind; e.Segments = 22;
        e.Fill = FillSpec.Solid(Magenta);
        return e;
    }

    static Element Progress(string name, string bind, float x, float y, float w, float h)
    {
        var e = Element.Create(ElementType.ChartLinear, 1, w, h);
        e.Name = name; e.X = x; e.Y = y; e.Width = w; e.Height = h;
        e.Bind = bind; e.CornerRadius = h / 2;
        e.Fill = Bar();
        e.RemainFill = FillSpec.Solid("#FF1B1B55");
        return e;
    }

    /// <summary>A name on the left, the value in white and the unit in magenta on the right.</summary>
    static void Row(List<Element> list, string name, float x, float y, float labelW, float valueRight, string valueToken, string unit, float size = 26)
    {
        list.Add(Text(name, name, x, y, labelW, 34, size, Label));
        list.Add(Text(name + " value", valueToken, valueRight - 160, y, 160, 34, size + 2, White, HAlign.Right));
        if (unit.Length > 0) list.Add(Text(name + " unit", unit, valueRight + 6, y + 4, 90, 30, size - 4, Magenta));
    }

    /// <summary>Ring with a title, the value and a caption inside.</summary>
    static void RingBlock(List<Element> list, string title, string bind, float x, float y, float size, string valueToken,
        string unitToken, string caption, float max = 100, bool unitIsPercent = false)
    {
        list.Add(Gauge(title + " ring", bind, x, y, size, max));
        list.Add(Text(title + " title", title, x, y + size * 0.20f, size, size * 0.27f, size * 0.25f, Pink, HAlign.Center, true, Title(), glow: 4));
        list.Add(Text(title + " value", valueToken, x + size * 0.03f, y + size * 0.47f, size * 0.56f, size * 0.28f, size * 0.23f, White, HAlign.Right, true));
        list.Add(Text(title + " unit", unitToken, x + size * 0.61f, y + size * 0.51f, size * 0.34f, size * 0.24f, size * (unitIsPercent ? 0.20f : 0.17f), Magenta, HAlign.Left, true));
        list.Add(Text(title + " caption", caption, x, y + size * 0.74f, size, size * 0.16f, size * 0.125f, Magenta, HAlign.Center));
    }

    public static Theme Create()
    {
        var t = new Theme
        {
            Name = "Neon Dashboard",
            Background = new FillSpec { Type = FillType.Radial, Color1 = "#FF0B0B52", Color2 = "#FF040420", ColorCount = 2 },
        };
        var l = t.Elements;

        // Frame and divider
        var frame = Element.Create(ElementType.Rectangle, 1, 0, 0);
        frame.Name = "Frame"; frame.X = 8; frame.Y = 6; frame.Width = 1904; frame.Height = 450;
        frame.CornerRadius = 46; frame.FillEnabled = false; frame.BorderEnabled = true; frame.BorderWidth = 4; frame.BorderColor = Blue;
        frame.GlowSize = 9; frame.GlowColor = Blue;
        l.Add(frame);
        var divider = Element.Create(ElementType.Line, 1, 0, 0);
        divider.Name = "Divider"; divider.X = 24; divider.Y = 231; divider.Width = 1872; divider.Height = 4;
        divider.BorderWidth = 3; divider.BorderColor = Blue; divider.GlowSize = 6; divider.GlowColor = Blue;
        l.Add(divider);

        // ---- top row: CPU | load | GPU ----
        RingBlock(l, "CPU", "cpu.temp", 30, 18, 200, "{cpu.temp}", "{unit}", "Temp", max: 80);
        l.Add(Text("CPU name", "{cpu.name}", 252, 14, 440, 26, 20, Blue));
        Row(l, "CPU Clock", 252, 44, 190, 570, "{cpu.clockmhz:0}", "MHz");
        Row(l, "CPU Fan", 252, 84, 190, 570, "{cpu.fan:0}", "RPM");
        Row(l, "CPU Power", 252, 124, 190, 570, "{cpu.power:0}", "W");
        l.Add(Text("CPU util", "Utilisation", 252, 174, 170, 34, 26, Label));
        l.Add(Bars("CPU bars", "cpu.load", 418, 172, 190, 36));
        l.Add(Text("CPU util value", "{cpu.load:0}", 606, 172, 62, 34, 28, White, HAlign.Right));
        l.Add(Text("CPU util unit", "%", 672, 176, 30, 30, 22, Magenta));

        l.Add(Text("Clock", "{time:h:mm:ss tt}", 690, 12, 540, 44, 36, White, HAlign.Center));
        RingBlock(l, "VRAM", "gpu.vrampct", 830, 58, 160, "{gpu.vrampct:0}", "%", "Memory", unitIsPercent: true);

        RingBlock(l, "GPU", "gpu.temp", 1186, 18, 200, "{gpu.temp}", "{unit}", "Temp", max: 80);
        l.Add(Text("GPU name", "{gpu.name}", 1412, 14, 470, 26, 20, Blue, HAlign.Right));
        Row(l, "GPU Clock", 1412, 44, 190, 1736, "{gpu.clockmhz:0}", "MHz");
        Row(l, "Memory", 1412, 84, 190, 1736, "{gpu.memclock:0}", "MHz");
        Row(l, "GPU Fan", 1412, 124, 190, 1736, "{gpu.fan:0}", "RPM");
        l.Add(Text("GPU util", "Utilisation", 1412, 174, 170, 34, 26, Label));
        l.Add(Bars("GPU bars", "gpu.load", 1578, 172, 190, 36));
        l.Add(Text("GPU util value", "{gpu.load:0}", 1766, 172, 62, 34, 28, White, HAlign.Right));
        l.Add(Text("GPU util unit", "%", 1832, 176, 30, 30, 22, Magenta));

        // ---- bottom row: board | storage | RAM ----
        RingBlock(l, "MOBO", "mobo.temp", 30, 244, 200, "{mobo.temp}", "{unit}", "Temp", max: 80);
        l.Add(Text("Board", "{mobo.name}", 252, 252, 440, 30, 24, Blue));
        Row(l, "Bios Version", 252, 302, 190, 600, "{bios.version}", "");
        Row(l, "Pch", 252, 346, 190, 600, "{pch.temp:0}", "{unit}");

        l.Add(Text("STORAGE", "STORAGE", 640, 240, 540, 52, 42, Pink, HAlign.Center, true, Title(), glow: 5));
        float y = 300;
        foreach (char drive in new[] { 'c', 'd', 'e' })
        {
            string up = char.ToUpperInvariant(drive).ToString();
            l.Add(Text($"Drive {up}", $"Drive {up}: Utilization", 700, y, 300, 28, 22, Label));
            l.Add(Text($"Drive {up} value", $"{{drive.{drive}.used:0}}", 1010, y, 70, 28, 24, White, HAlign.Right));
            l.Add(Text($"Drive {up} unit", "%", 1084, y + 3, 30, 26, 20, Magenta));
            l.Add(Progress($"Drive {up} bar", $"drive.{drive}.used", 702, y + 32, 410, 12));
            y += 50;
        }

        RingBlock(l, "RAM", "ram.load", 1186, 244, 200, "{ram.load:0}", "%", "Utilisation", unitIsPercent: true);
        Row(l, "Used", 1412, 262, 190, 1736, "{ram.usedmb:0}", "MB");
        Row(l, "Free", 1412, 304, 190, 1736, "{ram.freemb:0}", "MB");
        Row(l, "Speed", 1412, 346, 190, 1796, "{ram.type}-{ram.speed:0}", "");
        Row(l, "Clock", 1412, 388, 190, 1736, "{ram.clock:0}", "MHz");
        l.Add(Text("Date", "Date {time:M/d/yyyy}", 1640, 424, 240, 24, 17, Blue, HAlign.Right));
        return t;
    }
}
