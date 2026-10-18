using System.Text.Json;
using HurricaneBeastPanel.Ui;

namespace HurricaneBeastPanel;

/// <summary>One thing the user can add to a theme: one or more elements laid out in a nominal box.</summary>
public sealed class LibraryItem
{
    public string Name = "";
    public string Section = "";
    /// <summary>Size of the box the built elements are laid out in (also used to scale the preview).</summary>
    public float Width = 260, Height = 260;
    public Func<List<Element>> Build = () => new();
    /// <summary>Frames ask for an image / GIF / video right after they are added.</summary>
    public bool NeedsMedia;
    /// <summary>Set for the user's own saved groups so they can be deleted.</summary>
    public string? CustomFile;
}

public sealed class LibraryCategory
{
    public string Name = "";
    public IconKind Icon;
    public string Hint = "";
    public Func<List<LibraryItem>> Items = () => new();
}

public static class Library
{
    const string Cyan = "#FF00D2D2", Dim = "#FF8C97AC", White = "#FFFFFFFF";

    public static readonly IReadOnlyList<LibraryCategory> Categories = new[]
    {
        new LibraryCategory { Name = "Gauge", Icon = IconKind.Gauge, Hint = "Rings, arcs, dials and readouts", Items = () => WidgetItems(WidgetCategory.Gauge, ClassicGauges) },
        new LibraryCategory { Name = "Graph", Icon = IconKind.Graph, Hint = "Live history lines and bars", Items = () => WidgetItems(WidgetCategory.Graph, () => new()) },
        new LibraryCategory { Name = "Meter", Icon = IconKind.Meter, Hint = "Bars, tanks and level meters", Items = () => WidgetItems(WidgetCategory.Meter, ClassicMeters) },
        new LibraryCategory { Name = "Monitor", Icon = IconKind.Monitor, Hint = "Ready-made system monitor blocks", Items = MonitorItems },
        new LibraryCategory { Name = "Weather", Icon = IconKind.Weather, Hint = "Sun, rain and snow that follow the real weather", Items = WeatherItems },
        new LibraryCategory { Name = "Text", Icon = IconKind.Text, Hint = "Text, sensor values, dates and times", Items = TextItems },
        new LibraryCategory { Name = "Shape", Icon = IconKind.Shape, Hint = "Basic shapes and lines", Items = ShapeItems },
        new LibraryCategory { Name = "Frame", Icon = IconKind.Frame, Hint = "Image, GIF or video in a shaped frame", Items = FrameItems },
        new LibraryCategory { Name = "Plate", Icon = IconKind.Plate, Hint = "Card and panel backgrounds", Items = PlateItems },
        new LibraryCategory { Name = "Overlay", Icon = IconKind.Overlay, Hint = "HUD brackets, crosshairs and grids", Items = OverlayItems },
        new LibraryCategory { Name = "Custom", Icon = IconKind.Custom, Hint = "Your own saved groups", Items = CustomItems },
    };

    // ---------- demo data for previews ----------

    public static readonly SensorSnapshot Demo = new()
    {
        CpuName = "Intel Core i7", GpuName = "NVIDIA GeForce RTX",
        CpuTemp = 54, CpuLoad = 38, CpuPower = 65, CpuClockMhz = 4800, CpuFan = 1100,
        GpuTemp = 61, GpuLoad = 72, GpuPower = 180, GpuClockMhz = 1900, GpuFan = 1450, GpuMemUsedMb = 6200, GpuMemTotalMb = 12288,
        RamLoad = 47, RamUsedGb = 15.2f, RamTotalGb = 32, CaseFan = 900, PumpFan = 2400,
        NetUpBps = 250_000, NetDownBps = 1_800_000,
        MoboTemp = 38, PchTemp = 44, GpuHotspot = 72, GpuVoltage = 0.95f, CpuVoltage = 1.12f, GpuMemClockMhz = 1750, NetSentGb = 18.4f, NetRecvGb = 212.9f,
        BoardName = "Example Z790 Board", BiosVersion = "F17a", RamType = "DDR5", RamSpeedMts = 5600, NetIp = "192.168.1.20", NetName = "Ethernet",
        DisplayRes = "3840 x 2160", DisplayHz = 60, ProcessCount = 214,
        Drives =
        {
            new DriveSnap { Letter = 'C', Label = "", TotalGb = 139, UsedGb = 96 },
            new DriveSnap { Letter = 'D', Label = "Downloads", TotalGb = 812, UsedGb = 193 },
            new DriveSnap { Letter = 'E', Label = "Entertainment", TotalGb = 931, UsedGb = 333 },
            new DriveSnap { Letter = 'G', Label = "Games", TotalGb = 931, UsedGb = 212 },
        },
        Weather = new WeatherInfo(WeatherKind.PartlyCloudy, true, 21, 19, 24, 14, 55, 12, 20, "Partly cloudy", "London"),
    };

    // ---------- weather ----------

    static List<LibraryItem> WeatherItems()
    {
        const string Ink = "#FFFFFFFF", Soft = "#FF9FB0CC";
        Element Icon(float x, float y, float size, string force = "")
        {
            var e = Element.Create(ElementType.Weather, 1, size, size);
            e.Name = "Weather picture"; e.X = x; e.Y = y; e.Width = size; e.Height = size; e.Bind = force;
            return e;
        }
        Element Txt(string name, string text, float x, float y, float w, float h, float size, string color, HAlign align = HAlign.Left, bool bold = false)
        {
            var e = Theme.TextEl(name, text, x, y, w, h, size, bold, color, align);
            e.FontFamily = "Segoe UI Variable Display";
            return e;
        }

        var list = new List<LibraryItem>
        {
            new() { Name = "Weather picture", Width = 240, Height = 240, Build = () => new List<Element> { Icon(0, 0, 240) } },
            new()
            {
                Name = "Weather card", Width = 460, Height = 220,
                Build = () => new List<Element>
                {
                    Kit.Plate("Weather card", 0, 0, 460, 220, 26, Kit.Grad("#FF222A44", "#FF141828", 90), "#FF3A4570", 2),
                    Icon(12, 22, 176),
                    Txt("Weather temp", "{weather.temp:0}{unit}", 190, 18, 260, 96, 76, Ink, HAlign.Left, true),
                    Txt("Weather condition", "{weather.condition}", 194, 112, 256, 36, 26, Ink),
                    Txt("Weather city", "{weather.city}", 194, 148, 256, 28, 20, Soft),
                    Txt("Weather range", "H {weather.high:0}°   L {weather.low:0}°", 194, 176, 256, 28, 20, Soft),
                },
            },
            new()
            {
                Name = "Weather row", Width = 420, Height = 120,
                Build = () => new List<Element>
                {
                    Icon(0, 4, 112),
                    Txt("Weather temp", "{weather.temp:0}{unit}", 118, 2, 300, 64, 52, Ink, HAlign.Left, true),
                    Txt("Weather condition", "{weather.condition}", 122, 68, 296, 32, 24, Soft),
                },
            },
            new()
            {
                Name = "Weather details", Width = 300, Height = 150,
                Build = () => new List<Element>
                {
                    Txt("Humidity", "Humidity   {weather.humidity:0} %", 0, 0, 300, 34, 22, Ink),
                    Txt("Wind", "Wind   {weather.wind:0} km/h", 0, 38, 300, 34, 22, Ink),
                    Txt("Feels like", "Feels like   {weather.feels:0}{unit}", 0, 76, 300, 34, 22, Ink),
                    Txt("Rain chance", "Rain chance   {weather.rainchance:0} %", 0, 114, 300, 34, 22, Ink),
                },
            },
        };
        foreach (var (key, label) in WeatherRenderer.Choices.Where(c => c.key.Length > 0))
        {
            string k = key, l = label;
            list.Add(new LibraryItem { Name = l + " (fixed)", Section = "Fixed pictures", Width = 200, Height = 200, Build = () => new List<Element> { Icon(0, 0, 200, k) } });
        }
        return list;
    }

    // ---------- widgets ----------

    static List<LibraryItem> WidgetItems(WidgetCategory category, Func<List<LibraryItem>> extras)
    {
        var list = WidgetCatalog.In(category).Select(info => new LibraryItem
        {
            Name = info.Name, Width = info.Width, Height = info.Height,
            Build = () => new List<Element> { Element.CreateWidget(info.Style, 1, info.Width, info.Height) },
        }).ToList();
        list.AddRange(extras());
        return list;
    }

    static List<LibraryItem> ClassicGauges() => new()
    {
        Simple("Ring chart", 260, 260, () => Element.Create(ElementType.ChartCircular, 1, 260, 260)),
        Simple("Pie chart", 260, 260, () => Element.Create(ElementType.ChartPie, 1, 260, 260)),
    };

    static List<LibraryItem> ClassicMeters() => new()
    {
        Simple("Bar chart", 420, 40, () => Element.Create(ElementType.ChartLinear, 1, 420, 40)),
    };

    static LibraryItem Simple(string name, float w, float h, Func<Element> make, string section = "") => new()
    {
        Name = name, Width = w, Height = h, Section = section,
        Build = () =>
        {
            var e = make();
            e.X = 0; e.Y = 0; e.Width = w; e.Height = h;
            return new List<Element> { e };
        },
    };

    // ---------- monitor blocks ----------

    static List<LibraryItem> MonitorItems()
    {
        LibraryItem Card(string name, string title, string accent, string nameToken, string temp, string load, (string, string)[] rows) => new()
        {
            Name = name, Width = 420, Height = 414,
            Build = () => { var t = new Theme(); Theme.AddCard(t, 0, 0, 420, title, accent, nameToken, temp, load, rows); return t.Elements; },
        };

        LibraryItem Ring(string name, string label, string bind, string valueToken, string color, float min = 0, float max = 100) => new()
        {
            Name = name, Width = 260, Height = 260,
            Build = () =>
            {
                var gauge = Element.CreateWidget(WidgetStyle.ThreeQuarterArc, 1, 260, 260);
                gauge.X = 0; gauge.Y = 0; gauge.Name = label + " ring";
                gauge.Bind = bind; gauge.BindMin = min; gauge.BindMax = max;
                gauge.Fill = FillSpec.Solid(color);
                var value = Theme.TextEl(label + " value", valueToken, 30, 90, 200, 70, 54, true, align: HAlign.Center);
                var caption = Theme.TextEl(label + " label", label, 30, 150, 200, 36, 22, color: Dim, align: HAlign.Center);
                return new List<Element> { gauge, value, caption };
            },
        };

        LibraryItem GraphCard(string name, string title, string accent, string bind, string valueToken) => new()
        {
            Name = name, Width = 440, Height = 230,
            Build = () =>
            {
                var plate = Theme.RectEl(title + " plate", 0, 0, 440, 230, FillSpec.Solid("#FF181C28"), 26);
                var heading = Theme.TextEl(title + " title", title, 24, 14, 200, 44, 32, true, accent);
                var value = Theme.TextEl(title + " value", valueToken, 220, 14, 196, 44, 32, true, align: HAlign.Right);
                var graph = Element.CreateWidget(WidgetStyle.GradientFill, 1, 392, 140);
                graph.Name = title + " graph"; graph.X = 24; graph.Y = 68; graph.Width = 392; graph.Height = 144;
                graph.Bind = bind; graph.Fill = FillSpec.Solid(accent);
                return new List<Element> { plate, heading, value, graph };
            },
        };

        return new List<LibraryItem>
        {
            Card("CPU card", "CPU", "#FF5AAAFF", "{cpu.name}", "{cpu.temp}{unit}", "{cpu.load} %", Theme.CpuRows),
            Card("GPU card", "GPU", "#FFFF825A", "{gpu.name}", "{gpu.temp}{unit}", "{gpu.load} %", Theme.GpuRows),
            Card("Memory card", "MEMORY", "#FFAF87FF", "", "", "{ram.load} %", Theme.MemRows),
            new LibraryItem
            {
                Name = "Clock and date", Width = 520, Height = 300,
                Build = () => new List<Element>
                {
                    Theme.TextEl("Time", "{time}", 0, 0, 520, 190, 150, true),
                    Theme.TextEl("Date", "{date}", 0, 195, 520, 50, 34, color: Dim),
                    Theme.TextEl("Host", "{host}", 0, 246, 520, 36, 24, color: Dim),
                },
            },
            Ring("CPU temperature ring", "CPU", "cpu.temp", "{cpu.temp}{unit}", "#FF5AAAFF"),
            Ring("GPU temperature ring", "GPU", "gpu.temp", "{gpu.temp}{unit}", "#FFFF825A"),
            Ring("RAM usage ring", "RAM", "ram.load", "{ram.load}%", "#FFAF87FF"),
            Ring("GPU load ring", "GPU LOAD", "gpu.load", "{gpu.load}%", "#FF2BF28A"),
            GraphCard("CPU load graph", "CPU", "#FF5AAAFF", "cpu.load", "{cpu.load}%"),
            GraphCard("GPU load graph", "GPU", "#FFFF825A", "gpu.load", "{gpu.load}%"),
            GraphCard("Network graph", "DOWNLOAD", "#FF2BF28A", "net.down", "{net.down:0.0} Mbps"),
            new LibraryItem
            {
                Name = "Fan speeds", Width = 360, Height = 230,
                Build = () => new List<Element>
                {
                    Theme.RectEl("Fans plate", 0, 0, 360, 230, FillSpec.Solid("#FF181C28"), 26),
                    Theme.TextEl("Fans title", "FANS", 24, 14, 200, 44, 32, true, "#FF46DCDC"),
                    Theme.TextEl("CPU fan", "CPU fan", 24, 76, 160, 34, 24, color: Dim),
                    Theme.TextEl("CPU fan value", "{cpu.fan} RPM", 180, 76, 156, 34, 26, true, align: HAlign.Right),
                    Theme.TextEl("Case fan", "Case fan", 24, 118, 160, 34, 24, color: Dim),
                    Theme.TextEl("Case fan value", "{case.fan} RPM", 180, 118, 156, 34, 26, true, align: HAlign.Right),
                    Theme.TextEl("Pump", "Pump", 24, 160, 160, 34, 24, color: Dim),
                    Theme.TextEl("Pump value", "{pump.fan} RPM", 180, 160, 156, 34, 26, true, align: HAlign.Right),
                },
            },
            new LibraryItem
            {
                Name = "Status line", Width = 780, Height = 70,
                Build = () => new List<Element>
                {
                    Theme.TextEl("Status", "CPU {cpu.temp}{unit}   |   GPU {gpu.temp}{unit}   |   RAM {ram.load}%", 0, 0, 780, 70, 38, true, align: HAlign.Center),
                },
            },
        };
    }

    // ---------- text ----------

    static List<LibraryItem> TextItems()
    {
        var list = new List<LibraryItem>();
        LibraryItem T(string section, string name, string text, float w, float h, float size, bool bold = true, string color = White,
            HAlign align = HAlign.Center, FillSpec? fill = null) => new()
        {
            Section = section, Name = name, Width = w, Height = h,
            Build = () =>
            {
                var e = Theme.TextEl(name, text, 0, 0, w, h, size, bold, color, align);
                if (fill != null) e.Fill = fill;
                return new List<Element> { e };
            },
        };

        list.Add(T("Text", "Title", "Title", 520, 110, 84));
        list.Add(T("Text", "Heading", "Heading", 420, 80, 52));
        list.Add(T("Text", "Body", "Body text", 420, 60, 30, false, "#FFECF1FA"));
        list.Add(T("Text", "Caption", "Caption", 320, 44, 22, false, Dim));
        list.Add(T("Text", "Gradient title", "Gradient", 560, 110, 84, fill: new FillSpec
        {
            Type = FillType.Linear, Color1 = "#FF8B7BFF", Color2 = "#FFFF6EA8", Angle = 0,
        }));

        (string name, string text, float w, float size)[] sensors =
        {
            ("CPU temperature", "{cpu.temp}{unit}", 360, 90), ("CPU load", "{cpu.load} %", 320, 80), ("CPU power", "{cpu.power} W", 320, 80),
            ("CPU clock", "{cpu.clock} GHz", 420, 70), ("CPU fan", "{cpu.fan} RPM", 420, 70), ("CPU name", "{cpu.name}", 640, 36),
            ("GPU temperature", "{gpu.temp}{unit}", 360, 90), ("GPU load", "{gpu.load} %", 320, 80), ("GPU power", "{gpu.power} W", 320, 80),
            ("GPU clock", "{gpu.clock} GHz", 420, 70), ("GPU fan", "{gpu.fan} RPM", 420, 70), ("GPU name", "{gpu.name}", 640, 36),
            ("VRAM used", "{gpu.vram:0.0} / {gpu.vramtotal:0.0} GB", 640, 44), ("RAM load", "{ram.load} %", 320, 80),
            ("RAM used", "{ram.used:0.0} / {ram.total:0.0} GB", 640, 44), ("Upload", "{net.up:0.0} Mbps", 420, 56),
            ("Download", "{net.down:0.0} Mbps", 420, 56), ("Computer name", "{host}", 420, 50),
        };
        foreach (var (name, text, w, size) in sensors)
            list.Add(T("Sensor values", name, text, w, size * 1.4f, size));

        foreach (var group in DateTimeFormats.Groups)
        {
            foreach (var entry in group.Items)
            {
                float w = entry.IsPart ? 320 : Math.Clamp(entry.Format.Length * 36, 360, 1000), h = entry.IsPart ? 150 : 90;
                float size = entry.IsPart ? 110 : 56;
                string name = entry.IsPart ? entry.Label.Split('(')[0].Trim() : entry.Format;
                list.Add(T(group.Title.Replace(" (one at a time)", ""), name, DateTimeFormats.Token(entry.Format), w, h, size));
            }
        }
        list.Add(T("Date parts", "Day of year", "{dayofyear}", 320, 150, 110));
        list.Add(T("Date parts", "Week number", "{week}", 320, 150, 110));
        return list;
    }

    // ---------- shapes ----------

    static List<LibraryItem> ShapeItems()
    {
        LibraryItem S(string name, ElementType type, float w, float h, Action<Element>? tweak = null) => new()
        {
            Name = name, Width = w, Height = h,
            Build = () =>
            {
                var e = Element.Create(type, 1, w, h);
                e.X = 0; e.Y = 0; e.Width = w; e.Height = h; e.Name = name;
                tweak?.Invoke(e);
                return new List<Element> { e };
            },
        };

        FillSpec Grad(string a, string b, string? c = null, float angle = 45) => new()
        {
            Type = FillType.Linear, Color1 = a, Color2 = b, Color3 = c ?? b, ColorCount = c == null ? 2 : 3, Angle = angle,
        };

        return new List<LibraryItem>
        {
            S("Rectangle", ElementType.Rectangle, 280, 180),
            S("Rounded rectangle", ElementType.Rectangle, 280, 180, e => e.CornerRadius = 40),
            S("Circle", ElementType.Ellipse, 220, 220),
            S("Ellipse", ElementType.Ellipse, 300, 170),
            S("Triangle", ElementType.Triangle, 240, 210, e => e.CornerRadius = 14),
            S("Diamond", ElementType.Diamond, 220, 220, e => e.CornerRadius = 14),
            S("Pentagon", ElementType.Pentagon, 230, 220, e => e.CornerRadius = 14),
            S("Hexagon", ElementType.Hexagon, 260, 220, e => e.CornerRadius = 14),
            S("Line", ElementType.Line, 300, 12),
            S("Outline rectangle", ElementType.Rectangle, 280, 180, e => { e.FillEnabled = false; e.BorderEnabled = true; e.BorderWidth = 6; e.CornerRadius = 24; e.BorderColor = "#FF8B7BFF"; }),
            S("Outline circle", ElementType.Ellipse, 220, 220, e => { e.FillEnabled = false; e.BorderEnabled = true; e.BorderWidth = 6; e.BorderColor = "#FF8B7BFF"; }),
            S("Gradient rectangle", ElementType.Rectangle, 280, 180, e => { e.CornerRadius = 30; e.Fill = Grad("#FFFF5A5A", "#FFFFD25A", "#FF5AFF9A"); }),
            S("Gradient circle", ElementType.Ellipse, 220, 220, e => e.Fill = new FillSpec { Type = FillType.Radial, Color1 = "#FFFFFFFF", Color2 = "#FF3A7BFF", Color3 = "#FF101840", ColorCount = 3 }),
            S("Border + fill", ElementType.Rectangle, 280, 180, e => { e.CornerRadius = 30; e.BorderEnabled = true; e.BorderWidth = 8; e.BorderColor = "#FFFFFFFF"; e.Fill = Grad("#FF6C5CE7", "#FF8B7BFF"); }),
        };
    }

    // ---------- frames ----------

    static List<LibraryItem> FrameItems()
    {
        LibraryItem F(string name, ElementType shape, float w, float h, float radius = 0, bool border = false) => new()
        {
            Name = name, Width = w, Height = h, NeedsMedia = true,
            Build = () =>
            {
                var e = Element.Create(ElementType.Media, 1, w, h);
                e.X = 0; e.Y = 0; e.Width = w; e.Height = h; e.Name = name;
                e.FrameShape = shape; e.CornerRadius = radius;
                if (border) { e.BorderEnabled = true; e.BorderWidth = 8; e.BorderColor = "#FFFFFFFF"; }
                return new List<Element> { e };
            },
        };

        return new List<LibraryItem>
        {
            F("Rectangle frame", ElementType.Rectangle, 360, 240),
            F("Rounded frame", ElementType.Rectangle, 360, 240, 40),
            F("Circle frame", ElementType.Ellipse, 260, 260),
            F("Oval frame", ElementType.Ellipse, 360, 220),
            F("Triangle frame", ElementType.Triangle, 300, 260, 12),
            F("Diamond frame", ElementType.Diamond, 280, 280, 10),
            F("Pentagon frame", ElementType.Pentagon, 280, 270, 12),
            F("Hexagon frame", ElementType.Hexagon, 320, 280, 12),
            F("Rounded + border", ElementType.Rectangle, 360, 240, 40, true),
            F("Circle + border", ElementType.Ellipse, 260, 260, 0, true),
            F("Hexagon + border", ElementType.Hexagon, 320, 280, 14, true),
        };
    }

    // ---------- plates ----------

    static List<LibraryItem> PlateItems()
    {
        LibraryItem P(string name, float w, float h, Action<Element> tweak, ElementType type = ElementType.Rectangle) => new()
        {
            Name = name, Width = w, Height = h,
            Build = () =>
            {
                var e = Element.Create(type, 1, w, h);
                e.X = 0; e.Y = 0; e.Width = w; e.Height = h; e.Name = name; e.CornerRadius = 30;
                tweak(e);
                return new List<Element> { e };
            },
        };

        FillSpec Grad(string a, string b, float angle = 45, string? mid = null) => new()
        {
            Type = FillType.Linear, Color1 = a, Color2 = mid ?? b, Color3 = b, ColorCount = mid == null ? 2 : 3, Angle = angle,
        };

        return new List<LibraryItem>
        {
            P("Dark card", 380, 250, e => e.Fill = FillSpec.Solid("#FF1F1B3F")),
            P("Soft card", 380, 250, e => { e.Fill = Grad("#FF2E2960", "#FF1F1B3F", 90); e.BorderEnabled = true; e.BorderWidth = 2; e.BorderColor = "#FF3B3570"; }),
            P("Glass", 380, 250, e => { e.Fill = Grad("#44FFFFFF", "#0CFFFFFF", 90); e.BorderEnabled = true; e.BorderWidth = 2; e.BorderColor = "#55FFFFFF"; }),
            P("Purple gradient", 380, 250, e => e.Fill = Grad("#FF8B7BFF", "#FF5B4DF0")),
            P("Sunset", 380, 250, e => e.Fill = Grad("#FFFF7A59", "#FFFF3D81")),
            P("Ocean", 380, 250, e => e.Fill = Grad("#FF0F2027", "#FF2C5364", 60, "#FF203A43")),
            P("Neon outline", 380, 250, e => { e.FillEnabled = false; e.BorderEnabled = true; e.BorderWidth = 5; e.BorderColor = "#FF8B7BFF"; }),
            P("Pill", 380, 120, e => { e.CornerRadius = 60; e.Fill = FillSpec.Solid("#FF1F1B3F"); e.BorderEnabled = true; e.BorderWidth = 2; e.BorderColor = "#FF3B3570"; }),
            P("Circle plate", 260, 260, e => { e.Fill = Grad("#FF2E2960", "#FF1F1B3F", 90); e.BorderEnabled = true; e.BorderWidth = 3; e.BorderColor = "#FF8B7BFF"; }, ElementType.Ellipse),
            P("Hexagon plate", 300, 260, e => { e.CornerRadius = 20; e.Fill = Grad("#FF352E7A", "#FF1F1B3F", 90); e.BorderEnabled = true; e.BorderWidth = 3; e.BorderColor = "#FF8B7BFF"; }, ElementType.Hexagon),
        };
    }

    // ---------- overlays ----------

    static List<LibraryItem> OverlayItems()
    {
        Element L(string name, float x1, float y1, float x2, float y2, float width = 5, string color = "#FF8B7BFF")
        {
            // A line element is drawn along its box, so build the box from the two end points.
            var e = Element.Create(ElementType.Line, 1, 0, 0);
            e.Name = name;
            e.X = Math.Min(x1, x2); e.Y = Math.Min(y1, y2);
            e.Width = Math.Max(Math.Abs(x2 - x1), 1); e.Height = Math.Max(Math.Abs(y2 - y1), 1);
            e.BorderWidth = width; e.BorderColor = color;
            return e;
        }

        return new List<LibraryItem>
        {
            new LibraryItem
            {
                Name = "Corner brackets", Width = 320, Height = 220,
                Build = () =>
                {
                    const float w = 320, h = 220, a = 50;
                    return new List<Element>
                    {
                        L("Top left a", 0, 0, a, 0), L("Top left b", 0, 0, 0, a),
                        L("Top right a", w - a, 0, w, 0), L("Top right b", w, 0, w, a),
                        L("Bottom left a", 0, h, a, h), L("Bottom left b", 0, h - a, 0, h),
                        L("Bottom right a", w - a, h, w, h), L("Bottom right b", w, h - a, w, h),
                    };
                },
            },
            new LibraryItem
            {
                Name = "Crosshair", Width = 260, Height = 260,
                Build = () =>
                {
                    var ring = Element.Create(ElementType.Ellipse, 1, 0, 0);
                    ring.Name = "Crosshair ring"; ring.X = 50; ring.Y = 50; ring.Width = 160; ring.Height = 160;
                    ring.FillEnabled = false; ring.BorderEnabled = true; ring.BorderWidth = 4; ring.BorderColor = "#FF8B7BFF";
                    return new List<Element>
                    {
                        ring, L("Cross top", 130, 0, 130, 70, 4), L("Cross bottom", 130, 190, 130, 260, 4),
                        L("Cross left", 0, 130, 70, 130, 4), L("Cross right", 190, 130, 260, 130, 4),
                    };
                },
            },
            new LibraryItem
            {
                Name = "Grid", Width = 360, Height = 240,
                Build = () =>
                {
                    var list = new List<Element>();
                    for (int i = 0; i <= 6; i++) list.Add(L($"Grid v{i}", i * 60, 0, i * 60, 240, 2, "#448B7BFF"));
                    for (int i = 0; i <= 4; i++) list.Add(L($"Grid h{i}", 0, i * 60, 360, i * 60, 2, "#448B7BFF"));
                    return list;
                },
            },
            new LibraryItem { Name = "Divider", Width = 360, Height = 20, Build = () => new List<Element> { L("Divider", 0, 10, 360, 10, 4, "#FF3B3570") } },
            new LibraryItem
            {
                Name = "Accent line", Width = 360, Height = 20,
                Build = () =>
                {
                    var e = Theme.RectEl("Accent line", 0, 6, 360, 8, new FillSpec
                    {
                        Type = FillType.Linear, Color1 = "#FF5AAAFF", Color2 = "#FFAF87FF", Color3 = "#FFFF6EA8", ColorCount = 3,
                    }, 4);
                    return new List<Element> { e };
                },
            },
            new LibraryItem
            {
                Name = "Scan bars", Width = 320, Height = 160,
                Build = () =>
                {
                    var list = new List<Element>();
                    for (int i = 0; i < 8; i++)
                        list.Add(Theme.RectEl($"Scan bar {i + 1}", 0, i * 22, 320, 10, FillSpec.Solid($"#{(30 + i * 28):X2}8B7BFF"), 5));
                    return list;
                },
            },
        };
    }

    // ---------- custom (user saved) ----------

    public static string CustomDir => Path.Combine(AppSettings.Dir, "custom");

    sealed class CustomFile
    {
        public string Name { get; set; } = "";
        public List<Element> Elements { get; set; } = new();
    }

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    static (float x, float y, float w, float h) Bounds(IEnumerable<Element> elements)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var e in elements)
        {
            minX = Math.Min(minX, e.X); minY = Math.Min(minY, e.Y);
            maxX = Math.Max(maxX, e.X + e.Width); maxY = Math.Max(maxY, e.Y + e.Height);
        }
        return minX == float.MaxValue ? (0, 0, 100, 100) : (minX, minY, maxX - minX, maxY - minY);
    }

    static List<LibraryItem> CustomItems()
    {
        var list = new List<LibraryItem>();
        if (!Directory.Exists(CustomDir)) return list;
        foreach (string file in Directory.EnumerateFiles(CustomDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var data = JsonSerializer.Deserialize<CustomFile>(File.ReadAllText(file), Json);
                if (data == null || data.Elements.Count == 0) continue;
                var b = Bounds(data.Elements);
                string path = file;
                list.Add(new LibraryItem
                {
                    Name = data.Name, Width = b.w, Height = b.h, CustomFile = path,
                    Build = () =>
                    {
                        // Re-read so every insertion gets fresh copies, moved to the top-left of the box.
                        var fresh = JsonSerializer.Deserialize<CustomFile>(File.ReadAllText(path), Json)!.Elements;
                        var bb = Bounds(fresh);
                        foreach (var e in fresh) { e.X -= bb.x; e.Y -= bb.y; }
                        return fresh;
                    },
                });
            }
            catch (Exception ex) { Log.Write("Custom item skipped: " + ex.Message); }
        }
        return list;
    }

    /// <summary>Saves the given elements as a reusable group.</summary>
    public static void SaveCustom(string name, IEnumerable<Element> elements)
    {
        Directory.CreateDirectory(CustomDir);
        var copy = elements.Select(e => e.Clone()).ToList();
        string file = Path.Combine(CustomDir, ThemeStore.SafeName(name) + ".json");
        File.WriteAllText(file, JsonSerializer.Serialize(new CustomFile { Name = name, Elements = copy }, Json));
    }

    public static void DeleteCustom(LibraryItem item)
    {
        if (item.CustomFile != null) try { File.Delete(item.CustomFile); } catch { }
    }

    // ---------- previews ----------

    static string? _samplePath;

    /// <summary>A generated picture shown inside frame previews.</summary>
    static string SamplePicture()
    {
        if (_samplePath != null && File.Exists(_samplePath)) return _samplePath;
        string path = Path.Combine(AppSettings.Dir, "_sample.png");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(AppSettings.Dir);
            using var bmp = new Bitmap(400, 300);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var sky = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 400, 300), Color.FromArgb(255, 108, 92, 231), Color.FromArgb(255, 255, 122, 89), 70f))
                    g.FillRectangle(sky, 0, 0, 400, 300);
                using (var sun = new SolidBrush(Color.FromArgb(230, 255, 232, 160))) g.FillEllipse(sun, 250, 50, 90, 90);
                using (var hill1 = new SolidBrush(Color.FromArgb(255, 40, 30, 90))) g.FillPolygon(hill1, new[] { new Point(0, 300), new Point(0, 190), new Point(130, 130), new Point(260, 230), new Point(400, 170), new Point(400, 300) });
                using (var hill2 = new SolidBrush(Color.FromArgb(255, 22, 16, 56))) g.FillPolygon(hill2, new[] { new Point(0, 300), new Point(0, 250), new Point(110, 215), new Point(250, 265), new Point(400, 225), new Point(400, 300) });
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        return _samplePath = path;
    }

    /// <summary>Renders a preview of the item (width x height pixels, on black) using demo values.</summary>
    public static Bitmap RenderThumb(LibraryItem item, int width, int height)
    {
        var elements = item.Build().Select(e => e.Clone()).ToList();
        foreach (var e in elements)
        {
            if (e.IsWidget || e.IsChart) { e.Bind = e.Bind2 = e.Bind3 = ""; } // unbound widgets show demo values
            if (e.IsMedia) e.MediaPath = SamplePicture();
        }

        var b = Bounds(elements);
        float pad = Math.Max(b.w, b.h) * 0.06f + 6;
        var theme = new Theme
        {
            Width = (int)Math.Ceiling(b.w + pad * 2), Height = (int)Math.Ceiling(b.h + pad * 2),
            Background = FillSpec.Solid("#FF000000"),
        };
        foreach (var e in elements) { e.X += pad - b.x; e.Y += pad - b.y; theme.Elements.Add(e); }

        float scale = Math.Min(width / (float)theme.Width, height / (float)theme.Height);
        int rw = Math.Max(2, (int)(theme.Width * scale)), rh = Math.Max(2, (int)(theme.Height * scale));
        using var rendered = ThemeRenderer.Render(theme, Demo, new AppSettings(), rw, rh, preview: true);
        var result = new Bitmap(width, height);
        using var g = Graphics.FromImage(result);
        g.Clear(Color.Black);
        g.DrawImage(rendered, (width - rw) / 2, (height - rh) / 2, rw, rh);
        return result;
    }
}
