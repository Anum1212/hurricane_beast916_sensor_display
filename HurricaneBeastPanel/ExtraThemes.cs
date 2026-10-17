namespace HurricaneBeastPanel;

/// <summary>Small helpers for writing themes in code. Everything they make is an ordinary editable element.</summary>
static class Kit
{
    public static Element Txt(string name, string text, float x, float y, float w, float h, float size, string color,
        HAlign align = HAlign.Left, string font = "Segoe UI Variable Text", bool bold = false, bool italic = false, FillSpec? fill = null)
    {
        var e = Theme.TextEl(name, text, x, y, w, h, size, bold, color, align);
        e.FontFamily = font;
        e.Italic = italic;
        if (fill != null) e.Fill = fill;
        return e;
    }

    public static Element Plate(string name, float x, float y, float w, float h, float radius, FillSpec? fill = null,
        string? border = null, float borderWidth = 2)
    {
        var e = Element.Create(ElementType.Rectangle, 1, 0, 0);
        e.Name = name; e.X = x; e.Y = y; e.Width = w; e.Height = h; e.CornerRadius = radius;
        e.FillEnabled = fill != null;
        if (fill != null) e.Fill = fill;
        if (border != null) { e.BorderEnabled = true; e.BorderColor = border; e.BorderWidth = borderWidth; }
        return e;
    }

    public static Element HLine(string name, float x, float y, float w, float thickness, string color)
    {
        var e = Element.Create(ElementType.Line, 1, 0, 0);
        e.Name = name; e.X = x; e.Y = y; e.Width = w; e.Height = Math.Max(thickness, 2); e.BorderWidth = thickness; e.BorderColor = color;
        return e;
    }

    public static Element Bar(string name, string bind, float x, float y, float w, float h, FillSpec fill, string track,
        float min = 0, float max = 100)
    {
        var e = Element.Create(ElementType.ChartLinear, 1, w, h);
        e.Name = name; e.X = x; e.Y = y; e.Width = w; e.Height = h;
        e.Bind = bind; e.BindMin = min; e.BindMax = max; e.CornerRadius = h / 2;
        e.Fill = fill; e.RemainFill = FillSpec.Solid(track);
        return e;
    }

    public static Element Ring(string name, string bind, float x, float y, float size, FillSpec fill, string track,
        float thickness, float max = 100, WidgetStyle style = WidgetStyle.ThreeQuarterArc)
    {
        var e = Element.CreateWidget(style, 1, size, size);
        e.Name = name; e.X = x; e.Y = y; e.Width = size; e.Height = size;
        e.Bind = bind; e.BindMin = 0; e.BindMax = max;
        e.Fill = fill; e.RemainFill = FillSpec.Solid(track); e.RingThickness = thickness;
        return e;
    }

    public static Element Widget(WidgetStyle style, string name, string bind, float x, float y, float w, float h, FillSpec fill,
        string track = "#FF1E1E1E", float min = 0, float max = 100, int segments = 0)
    {
        var e = Element.CreateWidget(style, 1, w, h);
        e.Name = name; e.X = x; e.Y = y; e.Width = w; e.Height = h;
        e.Bind = bind; e.BindMin = min; e.BindMax = max;
        e.Fill = fill; e.RemainFill = FillSpec.Solid(track);
        if (segments > 0) e.Segments = segments;
        return e;
    }

    public static FillSpec Solid(string color) => FillSpec.Solid(color);

    public static FillSpec Grad(string a, string b, float angle = 0) => new()
    {
        Type = FillType.Linear, Color1 = a, Color2 = b, ColorCount = 2, Angle = angle,
    };

    /// <summary>A label on the left and a value on the right of one line.</summary>
    public static void Line(List<Element> list, string name, string label, string value, float x, float y, float w, float h,
        float size, string labelColor, string valueColor, string font = "Segoe UI Variable Text", bool boldValue = false,
        float valueShare = 0.55f)
    {
        list.Add(Txt(name, label, x, y, w * (1 - valueShare) + 10, h, size, labelColor, HAlign.Left, font));
        list.Add(Txt(name + " value", value, x + w * (1 - valueShare), y, w * valueShare, h, size, valueColor, HAlign.Right, font, boldValue));
    }
}

/// <summary>Built-in themes modelled on dashboards the user liked. Each is made only from editor elements.</summary>
public static class ExtraThemes
{
    public static readonly string[] Keys = { "tile", "cards", "compact", "smoky", "cardsh", "compacth", "rainbow", "rainbowh", "rainbowl", "rainbowhl" };

    /// <summary>
    /// Re-flows a vertical theme into columns on a 1920x462 canvas. Each slice is (y from, y to, x from, x to) of the
    /// vertical layout, the column it goes to and the new top of its first element.
    /// </summary>
    static Theme Reflow(Theme source, string name, float colPitch, params (float y0, float y1, float x0, float x1, int col, float top)[] slices)
    {
        var result = new Theme { Name = name, Width = 1920, Height = 462, Background = source.Background };
        var taken = new HashSet<Element>();
        foreach (var s in slices)
        {
            var group = source.Elements.Where(e => !taken.Contains(e) && e.Y >= s.y0 && e.Y < s.y1 && e.X >= s.x0 && e.X < s.x1).ToList();
            if (group.Count == 0) continue;
            float dy = s.top - s.y0;
            foreach (var e in group) { e.X += s.col * colPitch; e.Y += dy; taken.Add(e); result.Elements.Add(e); }
        }
        return result;
    }

    public static Theme CardsHorizontal()
    {
        const string Card = "#FF131313", Track = "#FF22262B", White = "#FFFFFFFF";
        const string Light = "Bahnschrift Light", Bold = "Bahnschrift SemiBold";
        var v = CardsVertical();
        const float P = 480;
        var t = Reflow(v, "Cards Horizontal", P,
            (0, 58, 0, 999, 0, 0),             // header
            (72, 354, 0, 999, 0, 62),          // CPU
            (1648, 1784, 0, 999, 0, 338),      // system
            (354, 634, 0, 999, 1, 0),          // GPU
            (1784, 1920, 0, 999, 1, 290),      // big clock
            (634, 884, 0, 236, 2, 0),          // RAM
            (634, 884, 236, 999, 2, 0),        // VRAM (right half)
            (884, 1124, 0, 236, 2, 242),       // MOBO
            (884, 1124, 236, 999, 2, 242),     // NET
            (1124, 1424, 0, 999, 3, 0));       // storage

        // Compact fans card in the 4th column
        var l = t.Elements;
        float gx = 10 + 3 * P, gy = 298;
        l.Add(Kit.Plate("Fans card", gx, gy, 442, 162, 4, Kit.Solid(Card)));
        l.Add(Kit.Txt("Fans title", "FANS", gx + 14, gy + 2, 200, 42, 32, "#FFB57BFF", HAlign.Left, Bold, true));
        l.Add(Kit.HLine("Fans line", gx + 4, gy + 44, 434, 3, "#FFB57BFF"));
        var fans = new[] { ("CPU fan", "cpu.fan", "{cpu.fan:0} RPM"), ("GPU fan", "gpu.fan", "{gpu.fan:0} RPM"), ("Case fan", "case.fan", "{case.fan:0} RPM"), ("Pump", "pump.fan", "{pump.fan:0} RPM") };
        for (int i = 0; i < 4; i++)
        {
            float cx = gx + 14 + (i % 2) * 216, cyy = gy + 52 + (i / 2) * 54;
            Kit.Line(l, fans[i].Item1, fans[i].Item1, fans[i].Item3, cx, cyy, 200, 28, 17, "#FFB0B0B0", White, Light, valueShare: 0.5f);
            l.Add(Kit.Bar(fans[i].Item1 + " bar", fans[i].Item2, cx, cyy + 30, 200, 4, Kit.Solid("#FFB57BFF"), Track, 0, 3000));
        }
        return t;
    }

    public static Theme CompactHorizontal()
    {
        var v = CompactVertical();
        const float P = 480;
        return Reflow(v, "Compact Horizontal", P,
            (0, 286, 0, 999, 0, 0),            // header + CPU
            (514, 692, 0, 999, 0, 286),        // board
            (286, 514, 0, 999, 1, 0),          // GPU
            (692, 892, 0, 999, 1, 228),        // cooling
            (892, 1092, 0, 999, 2, 0),         // memory
            (1708, 1920, 0, 999, 2, 200),      // network
            (1292, 1528, 0, 999, 3, 0),        // storage
            (1092, 1292, 0, 999, 3, 236));     // VRAM
    }

    public static Theme ByKey(string key) => key switch
    {
        "rainbow" => RainbowThemes.Build(false, true),
        "rainbowh" => RainbowThemes.Build(true, true),
        "rainbowl" => RainbowThemes.Build(false, false),
        "rainbowhl" => RainbowThemes.Build(true, false),
        "cardsh" => CardsHorizontal(),
        "compacth" => CompactHorizontal(),
        "tile" => TileDashboard(),
        "cards" => CardsVertical(),
        "compact" => CompactVertical(),
        "smoky" => SmokyBar(),
        "neon" => NeonTheme.Create(),
        _ => Theme.CreateDefault(),
    };

    public static IEnumerable<Theme> All() => Keys.Select(ByKey);

    // =====================================================================================================
    // 1. Tile dashboard (horizontal): black tiles with thin outlines, purple bars and gradient rings.
    // =====================================================================================================
    public static Theme TileDashboard()
    {
        const string Edge = "#FF3D3D3D", Dim = "#FFA3A3A3", White = "#FFFFFFFF", Track = "#FF2E2E2E", Purple = "#FFB36CFF";
        const string Title = "Segoe UI Variable Display", Num = "Bahnschrift SemiBold Condensed", Body = "Segoe UI Variable Text";
        var purple = Kit.Grad("#FF7A00D4", "#FFA94BFF");
        var ring = new FillSpec { Type = FillType.Linear, Color1 = "#FF7B00D4", Color2 = "#FFE80CC8", ColorCount = 2, Angle = 0 };

        var t = new Theme { Name = "Tile Dashboard", Background = Kit.Solid("#FF000000") };
        var l = t.Elements;

        // Draws a statistic as: bar on top, label left and value right underneath.
        void Stat(string name, string bind, string valueText, float x, float y, float w, float max = 100)
        {
            l.Add(Kit.Bar(name + " bar", bind, x, y, w, 10, purple, Track, 0, max));
            l.Add(Kit.Txt(name, name, x, y + 12, w * 0.6f, 26, 18, Dim, HAlign.Left, Body));
            l.Add(Kit.Txt(name + " value", valueText, x + w * 0.4f, y + 10, w * 0.6f, 28, 19, White, HAlign.Right, Body, true));
        }

        // --- column 1: clock, RAM, storage ---
        l.Add(Kit.Plate("Clock tile", 8, 8, 360, 124, 14, null, Edge));
        l.Add(Kit.Txt("Time", "{time:HH:mm:ss}", 8, 12, 360, 74, 58, White, HAlign.Center, "Segoe UI Light"));
        l.Add(Kit.Plate("Date pill", 64, 92, 248, 30, 8, Kit.Solid("#FFFFFFFF")));
        l.Add(Kit.Txt("Date", "{time:dd.MM.yyyy}", 64, 92, 248, 30, 19, "#FF000000", HAlign.Center, Body, true));

        l.Add(Kit.Plate("RAM tile", 8, 140, 360, 150, 14, null, Edge));
        l.Add(Kit.Txt("RAM title", "RAM", 24, 146, 200, 34, 26, White, HAlign.Left, Title));
        Stat("Load", "ram.load", "{ram.load:0} %", 24, 186, 328);
        l.Add(Kit.Txt("Speed", "Speed", 24, 226, 120, 24, 17, Dim, HAlign.Left, Body));
        l.Add(Kit.Txt("Speed value", "{ram.type}-{ram.speed}", 150, 226, 200, 24, 18, White, HAlign.Right, Body, true));
        l.Add(Kit.Txt("Usage", "Usage   {ram.used:0.0} GB       Free   {ram.total:0.0} GB", 24, 256, 328, 24, 17, Dim, HAlign.Left, Body));

        l.Add(Kit.Plate("Storage tile", 8, 298, 360, 156, 14, null, Edge));
        l.Add(Kit.Txt("Storage title", "Storage", 24, 304, 200, 34, 26, White, HAlign.Left, Title));
        float sy = 344;
        foreach (char d in new[] { 'c', 'd', 'e' })
        {
            string up = char.ToUpperInvariant(d).ToString();
            l.Add(Kit.Txt($"Drive {up}", $"{{drive.{d}.label}}", 24, sy, 170, 26, 16, Dim, HAlign.Left, Body));
            l.Add(Kit.Bar($"Drive {up} bar", $"drive.{d}.used", 190, sy + 7, 110, 10, purple, Track));
            l.Add(Kit.Txt($"Drive {up} free", $"{{drive.{d}.freegb:0}} GB", 296, sy, 62, 26, 16, White, HAlign.Right, Body, true));
            sy += 36;
        }

        // --- columns 2 and 3: CPU and GPU ---
        void Processor(string key, float x, string nameToken, string tempBind, string utilBind, string utilText, string clockBind, string clockText,
            string thirdLabel, string thirdBind, string thirdText, string fanBind, string fanText, float thirdMax = 100, float clockMax = 6000)
        {
            l.Add(Kit.Plate(key + " tile", x, 8, 598, 446, 16, null, Edge));
            l.Add(Kit.Txt(key + " name", nameToken, x + 22, 16, 560, 40, 28, White, HAlign.Left, Title));
            l.Add(Kit.Ring(key + " ring", tempBind, x + 14, 70, 270, ring, Track, 30, 100));
            l.Add(Kit.Txt(key + " temp", $"{{{tempBind}:0}}", x + 54, 140, 190, 120, 118, White, HAlign.Center, Num, true));
            l.Add(Kit.Txt(key + " unit", "{unit}", x + 54, 262, 190, 36, 28, Dim, HAlign.Center, Body));
            Stat($"{key} Utilization", utilBind, utilText, x + 316, 92, 262);
            Stat("Clock", clockBind, clockText, x + 316, 162, 262, clockMax);
            Stat(thirdLabel, thirdBind, thirdText, x + 316, 232, 262, thirdMax);
            Stat("Fan", fanBind, fanText, x + 316, 302, 262, 3000);
        }

        Processor("CPU", 376, "{cpu.name}", "cpu.temp", "cpu.load", "{cpu.load:0} %", "cpu.clockmhz", "{cpu.clockmhz:0} MHz",
            "Power", "cpu.power", "{cpu.power:0} W", "cpu.fan", "{cpu.fan:0} RPM", 200, 6000);
        l.Add(Kit.Txt("Uptime label", "Uptime", 400, 368, 120, 44, 30, Dim, HAlign.Left, Title));
        l.Add(Kit.Txt("Uptime", "{uptime}", 520, 368, 280, 44, 32, White, HAlign.Left, Title));

        Processor("GPU", 982, "{gpu.name}", "gpu.temp", "gpu.load", "{gpu.load:0} %", "gpu.clockmhz", "{gpu.clockmhz:0} MHz",
            "VRAM", "gpu.vrampct", "{gpu.vram:0.0} GB", "gpu.fan", "{gpu.fan:0} RPM", 100, 3000);

        // --- column 4: board, power, network ---
        l.Add(Kit.Plate("Board tile", 1588, 8, 324, 140, 14, null, Edge));
        l.Add(Kit.Txt("Board", "{mobo.name}", 1602, 14, 300, 30, 19, White, HAlign.Left, Title));
        l.Add(Kit.Txt("BIOS", "BIOS", 1602, 50, 80, 24, 16, Dim, HAlign.Left, Body));
        l.Add(Kit.Txt("BIOS value", "{bios.version}", 1680, 50, 220, 24, 17, White, HAlign.Right, Body, true));
        l.Add(Kit.Bar("Board temp bar", "mobo.temp", 1602, 90, 296, 10, purple, Track, 0, 90));
        l.Add(Kit.Txt("Temperature", "Temperature", 1602, 102, 160, 26, 17, Dim, HAlign.Left, Body));
        l.Add(Kit.Txt("Temperature value", "{mobo.temp:0} {unit}", 1740, 102, 158, 26, 19, White, HAlign.Right, Body, true));

        l.Add(Kit.Plate("Power tile", 1588, 156, 324, 122, 14, null, Edge));
        l.Add(Kit.Txt("Power title", "Power", 1602, 162, 200, 34, 24, White, HAlign.Left, Title));
        Kit.Line(l, "CPU power", "CPU", "{cpu.power:0.00} W", 1602, 202, 296, 28, 19, Dim, White, Body, true);
        Kit.Line(l, "GPU power", "GPU", "{gpu.power:0.00} W", 1602, 238, 296, 28, 19, Dim, White, Body, true);

        l.Add(Kit.Plate("Network tile", 1588, 286, 324, 168, 14, null, Edge));
        l.Add(Kit.Txt("Network", "{net.name}", 1602, 292, 120, 34, 22, White, HAlign.Left, Title));
        l.Add(Kit.Txt("IP", "{net.ip}", 1700, 292, 200, 34, 22, Purple, HAlign.Right, Title));
        Kit.Line(l, "Upload", "Upload", "{net.upmbs:0.0} MB/s", 1602, 332, 296, 24, 17, Dim, White, Body, true);
        Kit.Line(l, "Total up", "Total", "{net.sentgb:0.0} GB", 1602, 358, 296, 24, 17, Dim, White, Body, true);
        Kit.Line(l, "Download", "Download", "{net.downmbs:0.0} MB/s", 1602, 386, 296, 24, 17, Dim, White, Body, true);
        Kit.Line(l, "Total down", "Total", "{net.recvgb:0.0} GB", 1602, 412, 296, 24, 17, Dim, White, Body, true);
        return t;
    }

    // =====================================================================================================
    // 2. Cards (vertical): coloured sections with huge thin numbers and slim bars.
    // =====================================================================================================
    public static Theme CardsVertical()
    {
        const string Card = "#FF131313", Head = "#FF141414", Track = "#FF22262B", White = "#FFFFFFFF", Sub = "#FFD2D2D2";
        const string Light = "Bahnschrift Light", Bold = "Bahnschrift SemiBold", Cond = "Bahnschrift SemiBold Condensed";
        var t = new Theme { Name = "Cards Vertical", Width = 462, Height = 1920, Background = Kit.Solid("#FF000000") };
        var l = t.Elements;

        l.Add(Kit.Plate("Header", 0, 0, 462, 58, 0, Kit.Solid(Head)));
        l.Add(Kit.Txt("Time", "{time:HH:mm}", 16, 6, 140, 46, 32, White, HAlign.Left, Light));
        l.Add(Kit.Txt("Date", "{time:dd.MM.yyyy}", 130, 6, 220, 46, 32, White, HAlign.Center, Light));
        l.Add(Kit.Txt("Load", "{cpu.load:0}%", 340, 6, 108, 46, 32, White, HAlign.Right, Light));

        void Tri(string key, string title, string name, string accent, string accent2, float y, float h,
            string bind, string tempText, (string text, string bind, float max)[] rows)
        {
            l.Add(Kit.Plate(key + " card", 10, y, 442, h, 4, Kit.Solid(Card)));
            l.Add(Kit.Txt(key + " title", title, 24, y + 8, 200, 62, 52, accent, HAlign.Left, Bold, true));
            l.Add(Kit.Txt(key + " name", name, 26, y + 68, 330, 26, 18, Sub, HAlign.Left, Bold, true));
            var g = Kit.Widget(WidgetStyle.BarSparkline, key + " history", bind, 250, y + 18, 190, 46, Kit.Solid("#FF8A8A8A"), Card);
            g.Samples = 48; l.Add(g);
            l.Add(Kit.HLine(key + " line", 14, y + 100, 434, 3, accent));
            var level = Kit.Widget(WidgetStyle.SolidMeter, key + " level", bind, 26, y + 118, 12, h - 140, Kit.Grad(accent, accent2, 90), "#FF1E1E1E");
            l.Add(level);
            l.Add(Kit.Txt(key + " temp", tempText, 44, y + 108, 150, h - 118, 92, White, HAlign.Right, Light));
            l.Add(Kit.Txt(key + " unit", "{unit}", 196, y + 134, 56, 56, 34, accent, HAlign.Left, Light));
            float ry = y + 112, rowH = (h - 124) / rows.Length;
            foreach (var (text, rbind, max) in rows)
            {
                l.Add(Kit.Plate(key + " tick", 250, ry + 4, 3, rowH - 16, 0, Kit.Solid(accent)));
                l.Add(Kit.Txt(key + " row", text, 262, ry, 180, rowH - 10, 28, White, HAlign.Left, Light));
                l.Add(Kit.Bar(key + " row bar", rbind, 262, ry + rowH - 14, 176, 5, Kit.Solid(accent), Track, 0, max));
                ry += rowH;
            }
        }

        Tri("CPU", "CPU", "{cpu.name}", "#FF0A6CFF", "#FF9AE3FF", 72, 270, "cpu.load", "{cpu.temp:0}",
            new[] { ("{cpu.load:0} %", "cpu.load", 100f), ("{cpu.fan:0} RPM", "cpu.fan", 3000f), ("{cpu.power:0.0} W", "cpu.power", 200f) });
        Tri("GPU", "GPU", "{gpu.name}", "#FF0E9F68", "#FF3CF2B8", 354, 270, "gpu.load", "{gpu.temp:0}",
            new[] { ("{gpu.load:0} %", "gpu.load", 100f), ("{gpu.fan:0} RPM", "gpu.fan", 3000f), ("{gpu.power:0.0} W", "gpu.power", 400f) });

        // RAM and VRAM side by side
        void Half(string key, string title, string name, string accent, float x, float y, float h, string big, string bind,
            (string text, string bind, float max)[] rows)
        {
            l.Add(Kit.Plate(key + " card", x, y, 216, h, 4, Kit.Solid(Card)));
            l.Add(Kit.Txt(key + " title", title, x + 12, y + 6, 190, 54, 46, accent, HAlign.Left, Bold, true));
            l.Add(Kit.Txt(key + " name", name, x + 14, y + 58, 190, 22, 15, Sub, HAlign.Left, Bold, true));
            l.Add(Kit.HLine(key + " line", x + 6, y + 88, 204, 3, accent));
            if (rows.Length == 0)
            {
                l.Add(Kit.Txt(key + " big", big, x + 6, y + 100, 130, h - 110, 86, White, HAlign.Center, Light));
                l.Add(Kit.Txt(key + " unit", "%", x + 134, y + 112, 70, h - 124, 52, accent, HAlign.Left, Light));
                return;
            }
            float ry = y + 100, rowH = (h - 110) / rows.Length;
            foreach (var (text, rbind, max) in rows)
            {
                l.Add(Kit.Plate(key + " tick", x + 12, ry + 4, 3, rowH - 16, 0, Kit.Solid(accent)));
                l.Add(Kit.Txt(key + " row", text, x + 24, ry, 180, rowH - 10, 26, White, HAlign.Left, Light));
                l.Add(Kit.Bar(key + " row bar", rbind, x + 24, ry + rowH - 14, 174, 5, Kit.Solid(accent), Track, 0, max));
                ry += rowH;
            }
        }

        Half("RAM", "RAM", "{ram.type}-{ram.speed}", "#FFFF6A5A", 10, 634, 240, "{ram.load:0}", "ram.load", Array.Empty<(string, string, float)>());
        Half("VRAM", "VRAM", "{gpu.name}", "#FFFF6A5A", 236, 634, 240, "", "gpu.vrampct",
            new[] { ("{gpu.vrampct:0} %", "gpu.vrampct", 100f), ("{gpu.memclock:0} MHz", "gpu.memclock", 3000f), ("{gpu.hotspot:0} {unit}", "gpu.hotspot", 110f) });
        Half("MOBO", "MOBO", "{mobo.name}", "#FFFFB000", 10, 884, 220, "", "mobo.temp",
            new[] { ("PCH {pch.temp:0}°", "pch.temp", 100f), ("MB {mobo.temp:0}°", "mobo.temp", 100f) });
        Half("NET", "NET", "{net.name}", "#FFFFB000", 236, 884, 220, "", "net.down",
            new[] { ("↓ {net.downmbs:0.0} MB/s", "net.down", 1000f), ("↑ {net.upmbs:0.0} MB/s", "net.up", 1000f) });

        // Storage
        l.Add(Kit.Plate("Storage card", 10, 1124, 442, 290, 4, Kit.Solid(Card)));
        l.Add(Kit.Txt("Storage title", "STORAGE", 24, 1130, 300, 54, 44, "#FF22C3C3", HAlign.Left, Bold, true));
        l.Add(Kit.HLine("Storage line", 14, 1190, 434, 3, "#FF22C3C3"));
        float dy = 1204;
        foreach (char d in new[] { 'c', 'd', 'e', 'g' })
        {
            string up = char.ToUpperInvariant(d).ToString();
            l.Add(Kit.Txt($"Drive {up}", $"{{drive.{d}.label}}", 26, dy, 250, 34, 24, White, HAlign.Left, Light));
            l.Add(Kit.Txt($"Drive {up} free", $"{{drive.{d}.freegb:0}} GB", 270, dy, 168, 34, 24, White, HAlign.Right, Light));
            l.Add(Kit.Bar($"Drive {up} bar", $"drive.{d}.used", 26, dy + 36, 412, 6, Kit.Solid("#FF22C3C3"), Track));
            dy += 52;
        }

        // Fans
        l.Add(Kit.Plate("Fans card", 10, 1424, 442, 214, 4, Kit.Solid(Card)));
        l.Add(Kit.Txt("Fans title", "FANS", 24, 1430, 300, 54, 44, "#FFB57BFF", HAlign.Left, Bold, true));
        l.Add(Kit.HLine("Fans line", 14, 1490, 434, 3, "#FFB57BFF"));
        float fy = 1502;
        foreach (var (label, bind, token) in new[] { ("CPU fan", "cpu.fan", "{cpu.fan:0} RPM"), ("GPU fan", "gpu.fan", "{gpu.fan:0} RPM"), ("Case fan", "case.fan", "{case.fan:0} RPM"), ("Pump", "pump.fan", "{pump.fan:0} RPM") })
        {
            Kit.Line(l, label, label, token, 26, fy, 410, 30, 22, "#FFB0B0B0", White, Light);
            l.Add(Kit.Bar(label + " bar", bind, 26, fy + 28, 410, 4, Kit.Solid("#FFB57BFF"), Track, 0, 3000));
            fy += 34;
        }

        // System
        l.Add(Kit.Plate("System card", 10, 1648, 442, 124, 4, Kit.Solid(Card)));
        Kit.Line(l, "Uptime", "Uptime", "{uptime.days}", 26, 1656, 410, 34, 24, "#FFB0B0B0", White, Light);
        Kit.Line(l, "Processes", "Processes", "{sys.processes:0}", 26, 1690, 410, 34, 24, "#FFB0B0B0", White, Light);
        Kit.Line(l, "IP", "IP", "{net.ip}", 26, 1724, 410, 34, 24, "#FFB0B0B0", White, Light);

        // Big clock to fill the bottom
        l.Add(Kit.Txt("Big time", "{time:HH:mm}", 10, 1784, 442, 100, 84, White, HAlign.Center, Light));
        l.Add(Kit.Txt("Big date", "{time:dddd, d MMMM}", 10, 1876, 442, 36, 24, "#FF8A8A8A", HAlign.Center, Light));
        return t;
    }

    // =====================================================================================================
    // 3. Compact vertical: stacked blue-grey panels with titles, tables and purple bars.
    // =====================================================================================================
    public static Theme CompactVertical()
    {
        const string Dim = "#FFB4BCD0", White = "#FFFFFFFF", Track = "#FF2A2E48", Edge = "#FF6C7792";
        const string Title = "Segoe UI Variable Display", Body = "Segoe UI Variable Text", Num = "Bahnschrift SemiBold";
        var panel = new FillSpec { Type = FillType.Linear, Color1 = "#FF262A44", Color2 = "#FF171A2E", ColorCount = 2, Angle = 90 };
        var purple = Kit.Grad("#FF6A00D4", "#FFB44CFF");
        var t = new Theme
        {
            Name = "Compact Vertical", Width = 462, Height = 1920,
            Background = new FillSpec { Type = FillType.Linear, Color1 = "#FF0B0D1A", Color2 = "#FF11142A", ColorCount = 2, Angle = 90 },
        };
        var l = t.Elements;

        l.Add(Kit.Txt("Time", "{time:hh:mm tt}", 14, 8, 170, 40, 24, White, HAlign.Left, Body));
        l.Add(Kit.Txt("Date", "{time:yyyy.MM.dd}", 160, 8, 150, 40, 22, White, HAlign.Center, Body));
        l.Add(Kit.Txt("Net down", "↓ {net.downmbs:0.0}", 300, 8, 150, 20, 17, "#FF5EE6FF", HAlign.Right, Body));
        l.Add(Kit.Txt("Net up", "↑ {net.upmbs:0.0} MB/s", 300, 28, 150, 20, 17, "#FF5EE6FF", HAlign.Right, Body));

        float y = 58;
        void Panel(string title, string sub, float h)
        {
            l.Add(Kit.Plate(title + " panel", 10, y, 442, h, 18, panel, Edge, 2));
            l.Add(Kit.Txt(title + " title", title, 26, y + 8, 260, 36, 28, White, HAlign.Left, Title, true));
            if (sub.Length > 0) l.Add(Kit.Txt(title + " sub", sub, 26, y + 42, 400, 24, 16, Dim, HAlign.Left, Body));
        }

        void Row(string name, string label, string value, float rx, float ry, float rw, float size = 19)
        {
            l.Add(Kit.Plate(name + " tick", rx, ry + 3, 3, 22, 0, Kit.Solid("#FF8FA0C8")));
            l.Add(Kit.Txt(name, label, rx + 12, ry, rw * 0.5f, 28, size, Dim, HAlign.Left, Body));
            l.Add(Kit.Txt(name + " value", value, rx + rw * 0.4f, ry, rw * 0.6f, 28, size + 1, White, HAlign.Right, Body, true));
        }

        // CPU
        Panel("CPU", "{cpu.name}", 218);
        l.Add(Kit.Txt("CPU temp", "{cpu.temp:0}{unit}", 22, y + 74, 220, 96, 78, White, HAlign.Left, Num, true));
        l.Add(Kit.Txt("CPU range", "↓ {cpu.tempmin:0}°    ↑ {cpu.tempmax:0}°", 26, y + 176, 220, 30, 18, Dim, HAlign.Left, Body));
        Row("CPU load", "Load", "{cpu.load:0} %", 250, y + 76, 190);
        Row("CPU clock", "Clock", "{cpu.clock:0.00} GHz", 250, y + 112, 190);
        Row("CPU voltage", "Voltage", "{cpu.voltage:0.000} V", 250, y + 148, 190);
        Row("CPU power", "Power", "{cpu.power:0.0} W", 250, y + 184, 190);
        y += 228;

        // GPU
        Panel("GPU", "{gpu.name}", 218);
        l.Add(Kit.Txt("GPU temp", "{gpu.temp:0}{unit}", 22, y + 74, 220, 96, 78, White, HAlign.Left, Num, true));
        l.Add(Kit.Txt("GPU range", "↓ {gpu.tempmin:0}°    ↑ {gpu.tempmax:0}°", 26, y + 176, 220, 30, 18, Dim, HAlign.Left, Body));
        Row("GPU load", "Load", "{gpu.load:0} %", 250, y + 76, 190);
        Row("GPU clock", "Clock", "{gpu.clockmhz:0} MHz", 250, y + 112, 190);
        Row("GPU voltage", "Voltage", "{gpu.voltage:0.000} V", 250, y + 148, 190);
        Row("GPU power", "Power", "{gpu.power:0.0} W", 250, y + 184, 190);
        y += 228;

        // Board
        Panel("BOARD", "{mobo.name}", 168);
        l.Add(Kit.Txt("Board temp", "{mobo.temp:0}{unit}", 22, y + 70, 220, 84, 64, White, HAlign.Left, Num, true));
        l.Add(Kit.Txt("Board range", "↓ {mobo.tempmin:0}°    ↑ {mobo.tempmax:0}°", 26, y + 128, 220, 28, 17, Dim, HAlign.Left, Body));
        Row("BIOS", "BIOS", "{bios.version}", 250, y + 76, 190);
        Row("PCH", "Chipset", "{pch.temp:0} {unit}", 250, y + 112, 190);
        y += 178;

        // Cooling
        Panel("COOLING", "{mobo.name}", 190);
        float cy = y + 72;
        foreach (var (name, token) in new[] { ("Pump", "{pump.fan:0} RPM"), ("CPU fan", "{cpu.fan:0} RPM"), ("GPU fan", "{gpu.fan:0} RPM"), ("Case fan", "{case.fan:0} RPM") })
        {
            Row(name, name, token, 26, cy, 410);
            cy += 28;
        }
        y += 200;

        // Memory and VRAM with purple bars
        void Usage(string title, string sub, string bind, string valueText, string rowA, string rowB, float h)
        {
            Panel(title, sub, h);
            l.Add(Kit.Bar(title + " bar", bind, 26, y + 76, 410, 22, purple, Track));
            l.Add(Kit.Txt(title + " used", valueText, 26, y + 100, 410, 28, 20, White, HAlign.Right, Body, true));
            Row(title + " a", rowA.Split('|')[0], rowA.Split('|')[1], 26, y + 128, 410);
            Row(title + " b", rowB.Split('|')[0], rowB.Split('|')[1], 26, y + 158, 410);
            y += h + 10;
        }
        Usage("MEMORY", "{ram.type}-{ram.speed}", "ram.load", "{ram.used:0.0} / {ram.total:0.0} GB", "Load|{ram.load:0} %", "Clock|{ram.clock:0} MHz", 190);
        Usage("VRAM", "{gpu.name}", "gpu.vrampct", "{gpu.vram:0.0} / {gpu.vramtotal:0.0} GB", "Load|{gpu.vrampct:0} %", "Memory clock|{gpu.memclock:0} MHz", 190);

        // Storage
        Panel("STORAGE", "Internal drives", 226);
        float sy = y + 66;
        foreach (char d in new[] { 'c', 'd', 'e', 'g' })
        {
            string up = char.ToUpperInvariant(d).ToString();
            l.Add(Kit.Txt($"Drive {up}", $"{{drive.{d}.label}}", 26, sy, 250, 24, 17, Dim, HAlign.Left, Body));
            l.Add(Kit.Txt($"Drive {up} free", $"{{drive.{d}.freegb:0}} GB free", 250, sy, 186, 24, 17, White, HAlign.Right, Body, true));
            l.Add(Kit.Bar($"Drive {up} bar", $"drive.{d}.used", 26, sy + 26, 410, 10, purple, Track));
            sy += 38;
        }
        y += 236;

        // System
        Panel("SYSTEM", "{host}  |  main monitor", 170);
        Row("Resolution", "Resolution", "{display.res}", 26, y + 66, 410);
        Row("Refresh", "Refresh rate", "{display.hz:0} Hz", 26, y + 92, 410);
        Row("Uptime", "Uptime", "{uptime.days}", 26, y + 118, 410);
        Row("Processes", "Processes", "{sys.processes:0}", 26, y + 144, 410);
        y += 180;

        Panel("NETWORK", "{net.name}  {net.ip}", 170);
        Row("Down", "Download", "{net.downmbs:0.0} MB/s", 26, y + 64, 410);
        Row("Down total", "Total", "{net.recvgb:0.0} GB", 26, y + 90, 410);
        Row("Up", "Upload", "{net.upmbs:0.0} MB/s", 26, y + 116, 410);
        Row("Up total", "Total", "{net.sentgb:0.0} GB", 26, y + 142, 410);
        return t;
    }

    // =====================================================================================================
    // 4. Smoky bar (horizontal): light thin text on a misty dark background picture.
    // =====================================================================================================
    public static Theme SmokyBar()
    {
        const string Gray = "#FFC9D0D8", White = "#FFFFFFFF", Ice = "#FF7FD0FF", Track = "#7F16202C";
        const string Thin = "Bahnschrift Light", Cond = "Bahnschrift SemiLight Condensed", Semi = "Bahnschrift SemiLight";
        var t = new Theme
        {
            Name = "Smoky Bar", Background = Kit.Grad("#FF05080F", "#FF0B1424", 90),
            BackgroundMedia = BackgroundArt.EnsureSmoky(), BackgroundMediaFit = MediaFit.Cover,
        };
        var l = t.Elements;
        var iceFill = Kit.Solid(Ice);

        void Temp(string key, string label, string bind, string token, float x, float y, float width, float size, bool withBar = true)
        {
            l.Add(Kit.Txt(key + " label", label, x, y, width, 22, 17, Gray, HAlign.Left, Thin));
            l.Add(Kit.Txt(key, token, x, y + 14, width * 0.62f, size * 1.15f, size, White, HAlign.Left, Cond, true));
            l.Add(Kit.Txt(key + " unit", "{unit}", x + size * 1.18f, y + 22, width * 0.4f, size * 0.7f, size * 0.52f, Ice, HAlign.Left, Thin));
            if (withBar) l.Add(Kit.Bar(key + " bar", bind, x, y + 14 + size * 1.16f, width * 0.8f, 8, Kit.Solid("#FF8FD8FF"), Track, 0, 100));
        }

        // Left: temperatures and fans
        l.Add(Kit.Txt("Temps title", "SYSTEM TEMPS", 40, 26, 360, 28, 24, White, HAlign.Left, Semi));
        Temp("CPU temp", "CPU PACKAGE", "cpu.temp", "{cpu.temp:0}", 40, 66, 300, 54, false);
        Temp("GPU temp", "GPU CORE", "gpu.temp", "{gpu.temp:0}", 40, 144, 300, 54, false);
        l.Add(Kit.Txt("Pump label", "PUMP", 40, 224, 140, 28, 20, Gray, HAlign.Left, Thin));
        l.Add(Kit.Txt("Pump", "{pump.fan:0} RPM", 150, 224, 190, 28, 22, Ice, HAlign.Left, Thin));

        l.Add(Kit.Txt("Fans title", "FAN RPM", 40, 282, 300, 28, 24, White, HAlign.Left, Semi));
        float fy = 320;
        foreach (var (label, token) in new[] { ("CPU FAN", "{cpu.fan:0}"), ("GPU FAN", "{gpu.fan:0}"), ("CASE FAN", "{case.fan:0}") })
        {
            l.Add(Kit.Txt(label, label, 40, fy, 170, 28, 20, Gray, HAlign.Left, Thin));
            l.Add(Kit.Txt(label + " value", token + " RPM", 200, fy, 140, 28, 21, Ice, HAlign.Right, Thin));
            fy += 36;
        }

        // CPU column
        l.Add(Kit.Txt("CPU name", "{cpu.name}", 400, 22, 420, 30, 24, White, HAlign.Center, Thin));
        l.Add(Kit.Widget(WidgetStyle.LcdBlocks, "CPU load blocks", "cpu.load", 400, 60, 330, 24, Kit.Solid("#FF3A5A7A"), "#FF16202C", 0, 100, 40));
        l.Add(Kit.Txt("CPU load", "{cpu.load:0} %", 740, 56, 90, 30, 20, Ice, HAlign.Right, Thin));
        Temp("CPU big", "", "cpu.temp", "{cpu.temp:0}", 400, 92, 300, 76, true);
        Kit.Line(l, "CPU power", "CPU POWER", "{cpu.power:0.00} W", 400, 202, 420, 26, 18, Gray, Ice, Thin);
        float cy = 250;
        foreach (var (label, token) in new[] { ("CHIPSET", "{pch.temp:0}"), ("MB TEMP", "{mobo.temp:0}"), ("CPU VOLT", "{cpu.voltage:0.00}"), ("PROCESSES", "{sys.processes:0}") })
        {
            l.Add(Kit.Txt(label, label, 400, cy, 220, 28, 20, Gray, HAlign.Left, Thin));
            l.Add(Kit.Txt(label + " value", token, 640, cy, 150, 28, 22, White, HAlign.Right, Cond, true));
            cy += 36;
        }

        // GPU column
        l.Add(Kit.Txt("GPU name", "{gpu.name}", 900, 22, 440, 30, 24, White, HAlign.Center, Thin));
        l.Add(Kit.Widget(WidgetStyle.LcdBlocks, "GPU load blocks", "gpu.load", 900, 60, 330, 24, Kit.Solid("#FF3A5A7A"), "#FF16202C", 0, 100, 40));
        l.Add(Kit.Txt("GPU load", "{gpu.load:0} %", 1240, 56, 90, 30, 20, Ice, HAlign.Right, Thin));
        Temp("GPU big", "", "gpu.temp", "{gpu.temp:0}", 900, 92, 300, 76, true);
        Kit.Line(l, "GPU power", "GPU POWER", "{gpu.power:0.00} W", 900, 202, 430, 26, 18, Gray, Ice, Thin);
        Temp("GPU hotspot", "GPU HOTSPOT", "gpu.hotspot", "{gpu.hotspot:0}", 900, 262, 300, 64, true);

        // Memory
        l.Add(Kit.Txt("RAM title", "{ram.type}-{ram.speed}  MEMORY", 1390, 22, 280, 30, 22, White, HAlign.Center, Thin));
        l.Add(Kit.Bar("RAM bar", "ram.load", 1390, 70, 280, 14, Kit.Solid("#FFA0E0FF"), "#FF1B2A3A"));
        l.Add(Kit.Txt("RAM used", "{ram.used:0.0} GB", 1390, 90, 130, 28, 20, Ice, HAlign.Left, Thin));
        l.Add(Kit.Txt("RAM total", "{ram.total:0.0} GB", 1540, 90, 130, 28, 20, Ice, HAlign.Right, Thin));

        // Network
        l.Add(Kit.Txt("Net title", "NETWORK", 1720, 22, 180, 28, 22, White, HAlign.Center, Thin));
        Kit.Line(l, "Down", "DOWN", "{net.downmbs:0.0} MB/s", 1720, 60, 180, 26, 17, Gray, Ice, Thin, valueShare: 0.62f);
        Kit.Line(l, "Up", "UP", "{net.upmbs:0.0} MB/s", 1720, 90, 180, 26, 17, Gray, Ice, Thin, valueShare: 0.62f);
        Kit.Line(l, "Uptime", "UPTIME", "{uptime}", 1720, 120, 180, 26, 17, Gray, White, Thin, valueShare: 0.62f);

        // Big clock where the reference shows an FPS counter
        l.Add(Kit.Txt("Big time", "{time:HH:mm}", 1370, 250, 540, 130, 124, Ice, HAlign.Right, Cond, true));
        l.Add(Kit.Txt("Big date", "{time:dddd, d MMMM}", 1370, 384, 540, 40, 28, White, HAlign.Right, Thin));
        return t;
    }
}

/// <summary>Generates background pictures used by built-in themes.</summary>
static class BackgroundArt
{
    /// <summary>A dark blue misty picture (made on the fly so no image file has to ship with the app). Returns the stored path.</summary>
    public static string EnsureSmoky()
    {
        string file = Path.Combine(MediaCache.MediaDir, "smoky_background.png");
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(MediaCache.MediaDir);
            const int w = 1920, h = 462;
            using var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var bg = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(255, 4, 7, 14), Color.FromArgb(255, 12, 22, 40), 20f))
                g.FillRectangle(bg, 0, 0, w, h);
            var rng = new Random(7);
            for (int i = 0; i < 70; i++)
            {
                float cx = (float)(rng.NextDouble() * w), cy = (float)(rng.NextDouble() * h);
                float rx = 120 + (float)rng.NextDouble() * 360, ry = 30 + (float)rng.NextDouble() * 120;
                float angle = (float)(rng.NextDouble() * 60 - 30);
                int alpha = 6 + rng.Next(18);
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddEllipse(-rx, -ry, rx * 2, ry * 2);
                using var brush = new System.Drawing.Drawing2D.PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(alpha, 150, 190, 230),
                    SurroundColors = new[] { Color.FromArgb(0, 150, 190, 230) },
                };
                var state = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(angle);
                g.FillPath(brush, path);
                g.Restore(state);
            }
            // Darken the edges so text near the border stays readable.
            using (var vignette = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(120, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 0f))
            {
                vignette.SetBlendTriangularShape(0.5f, 0f);
                g.FillRectangle(vignette, 0, 0, w, h);
            }
            bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
        }
        return Path.GetRelativePath(AppSettings.Dir, file);
    }
}
