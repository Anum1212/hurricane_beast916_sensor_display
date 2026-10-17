using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HurricaneBeastPanel;

public enum FillType { Solid, Linear, Radial }

public enum ElementType
{
    Text, Rectangle, Ellipse, Triangle, Diamond, Pentagon, Hexagon, Line,
    ChartCircular, ChartPie, ChartLinear, Media,
    /// <summary>A gauge, graph or meter from the widget library; <see cref="Element.Style"/> picks which.</summary>
    Widget,
}

public enum HAlign { Left, Center, Right }

/// <summary>How an image or video is fitted into its frame.</summary>
public enum MediaFit { Cover, Contain, Stretch }

public static class ColorUtil
{
    /// <summary>Parses #RRGGBB or #AARRGGBB.</summary>
    public static Color Parse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.Magenta;
        string h = hex.Trim().TrimStart('#');
        if (h.Length == 6) h = "FF" + h;
        if (h.Length == 8 && uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            return Color.FromArgb((int)v);
        return Color.Magenta;
    }

    public static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}

/// <summary>Solid colour or a 2-3 colour linear/radial gradient.</summary>
public sealed class FillSpec
{
    public FillType Type { get; set; } = FillType.Solid;
    public string Color1 { get; set; } = "#FFFFFFFF";
    public string Color2 { get; set; } = "#FF3A7BFF";
    public string Color3 { get; set; } = "#FFB04BFF";
    /// <summary>Number of colours used by gradients (2 or 3).</summary>
    public int ColorCount { get; set; } = 2;
    /// <summary>Direction of linear gradients in degrees (0 = left to right, 90 = top to bottom).</summary>
    public float Angle { get; set; }

    public static FillSpec Solid(string color) => new() { Type = FillType.Solid, Color1 = color };

    public FillSpec Clone() => (FillSpec)MemberwiseClone();

    /// <summary>Builds a brush for the given bounds. A radial gradient fits the bounds' ellipse when
    /// <paramref name="inscribed"/> is set, otherwise it reaches the corners. Caller disposes.</summary>
    public Brush CreateBrush(RectangleF r, bool inscribed = false)
    {
        Color c1 = ColorUtil.Parse(Color1), c2 = ColorUtil.Parse(Color2), c3 = ColorUtil.Parse(Color3);
        bool three = ColorCount >= 3;
        if (Type == FillType.Solid || r.Width < 1 || r.Height < 1)
            return new SolidBrush(c1);

        if (Type == FillType.Linear)
        {
            var lb = new LinearGradientBrush(r, c1, three ? c3 : c2, Angle, false);
            if (three)
            {
                lb.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { c1, c2, c3 },
                    Positions = new[] { 0f, 0.5f, 1f },
                };
            }
            lb.WrapMode = WrapMode.TileFlipXY;
            return lb;
        }

        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        float k = inscribed ? 1f : 1.4143f;
        float rx = r.Width / 2 * k, ry = r.Height / 2 * k;
        using var path = new GraphicsPath();
        path.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
        var pg = new PathGradientBrush(path) { CenterPoint = new PointF(cx, cy) };
        // PathGradientBrush positions run from the edge (0) to the centre (1).
        pg.InterpolationColors = three
            ? new ColorBlend { Colors = new[] { c3, c2, c1 }, Positions = new[] { 0f, 0.5f, 1f } }
            : new ColorBlend { Colors = new[] { c2, c1 }, Positions = new[] { 0f, 1f } };
        return pg;
    }
}

public sealed class Element
{
    public string Name { get; set; } = "";
    public ElementType Type { get; set; }
    public bool Visible { get; set; } = true;
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = 200;
    public float Height { get; set; } = 100;
    /// <summary>Clockwise rotation in degrees around the element's centre.</summary>
    public float Rotation { get; set; }

    // Text
    public string Text { get; set; } = "Text";
    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 40;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public HAlign Align { get; set; } = HAlign.Left;

    // Shapes and charts. Text uses Fill as its colour; charts use it for the filled part.
    public bool FillEnabled { get; set; } = true;
    public FillSpec Fill { get; set; } = FillSpec.Solid("#FF3A7BFF");
    public bool BorderEnabled { get; set; }
    public float BorderWidth { get; set; } = 4;
    public string BorderColor { get; set; } = "#FFFFFFFF";
    public float CornerRadius { get; set; }

    // Live value: charts show it, shapes use it to scale their width (a bar).
    public string Bind { get; set; } = "";
    public float BindMin { get; set; }
    public float BindMax { get; set; } = 100;

    // Charts
    public bool RemainEnabled { get; set; } = true;
    public FillSpec RemainFill { get; set; } = FillSpec.Solid("#FF2E3548");
    public float RingThickness { get; set; } = 40;
    public float StartAngle { get; set; } = 135;
    public float SweepAngle { get; set; } = 270;

    /// <summary>Soft outer glow around text, shapes and lines (0 = off).</summary>
    public float GlowSize { get; set; }
    public string GlowColor { get; set; } = "#FF3A7BFF";

    // Widgets (gauges, graphs, meters). Fill = main colour, RemainFill = track, BindMin/BindMax apply to every input.
    public WidgetStyle Style { get; set; } = WidgetStyle.ThreeQuarterArc;
    public string Bind2 { get; set; } = "";
    public string Bind3 { get; set; } = "";
    /// <summary>Second colour used by needles, zones, the inner ring of dual gauges and similar details.</summary>
    public string AccentColor { get; set; } = "#FFFF4D4D";
    /// <summary>Number of segments, ticks, dots, LED blocks or matrix rows (depends on the style).</summary>
    public int Segments { get; set; } = 12;
    /// <summary>Number of history samples shown by graphs (about one per second).</summary>
    public int Samples { get; set; } = 60;
    public float LineWidth { get; set; } = 3;

    // Media (image, GIF or video)
    public string MediaPath { get; set; } = "";
    public MediaFit Fit { get; set; } = MediaFit.Cover;
    /// <summary>Shape of the frame the media is shown in.</summary>
    public ElementType FrameShape { get; set; } = ElementType.Rectangle;

    [JsonIgnore] public bool IsText => Type == ElementType.Text;
    [JsonIgnore] public bool IsLine => Type == ElementType.Line;
    [JsonIgnore] public bool IsMedia => Type == ElementType.Media;
    [JsonIgnore] public bool IsWidget => Type == ElementType.Widget;
    [JsonIgnore] public bool SupportsGlow => IsText || IsLine || Type is ElementType.Rectangle or ElementType.Ellipse
        or ElementType.Triangle or ElementType.Diamond or ElementType.Pentagon or ElementType.Hexagon;
    [JsonIgnore] public bool IsChart => Type is ElementType.ChartCircular or ElementType.ChartPie or ElementType.ChartLinear;
    [JsonIgnore] public bool HasCorners => Type is ElementType.Rectangle or ElementType.Triangle
        or ElementType.Diamond or ElementType.Pentagon or ElementType.Hexagon or ElementType.ChartLinear
        || (IsMedia && FrameShape != ElementType.Ellipse);

    public RectangleF Bounds => new(X, Y, Width, Height);
    public PointF Center => new(X + Width / 2, Y + Height / 2);

    public static Element Create(ElementType type, int index, float canvasW, float canvasH)
    {
        var e = new Element { Type = type, Name = $"{type} {index}" };
        switch (type)
        {
            case ElementType.Text:
                e.Width = 400; e.Height = 80;
                e.Fill = FillSpec.Solid("#FFFFFFFF");
                break;
            case ElementType.Line:
                e.Width = 300; e.Height = 10;
                e.BorderEnabled = true; e.BorderWidth = 6; e.FillEnabled = false;
                break;
            case ElementType.ChartCircular:
                e.Width = 260; e.Height = 260; e.Bind = "cpu.load";
                e.Fill = new FillSpec { Type = FillType.Linear, Color1 = "#FF5AAAFF", Color2 = "#FFAF87FF", Angle = 45 };
                break;
            case ElementType.ChartPie:
                e.Width = 260; e.Height = 260; e.Bind = "cpu.load";
                e.StartAngle = -90; e.SweepAngle = 360;
                e.Fill = new FillSpec { Type = FillType.Linear, Color1 = "#FFFF825A", Color2 = "#FFFFD25A", Angle = 45 };
                break;
            case ElementType.ChartLinear:
                e.Width = 420; e.Height = 40; e.Bind = "cpu.load"; e.CornerRadius = 20;
                e.Fill = new FillSpec { Type = FillType.Linear, Color1 = "#FF46DC8C", Color2 = "#FFFFD25A", Angle = 0 };
                break;
            case ElementType.Media:
                e.Width = 400; e.Height = 300; e.FillEnabled = false;
                break;
            default:
                e.Width = 200; e.Height = 200;
                break;
        }
        e.X = MathF.Round((canvasW - e.Width) / 2);
        e.Y = MathF.Round((canvasH - e.Height) / 2);
        return e;
    }

    /// <summary>A new gauge, graph or meter with the catalog's default size, colours and settings.</summary>
    public static Element CreateWidget(WidgetStyle style, int index, float canvasW, float canvasH)
    {
        var info = WidgetCatalog.Info(style);
        var e = new Element
        {
            Type = ElementType.Widget, Style = style, Name = $"{info.Name} {index}",
            Width = info.Width, Height = info.Height,
            StartAngle = info.Start, SweepAngle = info.Sweep, RingThickness = Math.Max(1, info.Thickness),
            Segments = info.Segments, Samples = info.Samples,
            Fill = info.Fill?.Clone() ?? FillSpec.Solid("#FF00D2D2"),
            RemainFill = FillSpec.Solid(info.Track), AccentColor = info.Accent,
            Bind = "cpu.load", Bind2 = info.Bind2 ? "ram.load" : "", Bind3 = info.Bind3 ? "gpu.load" : "",
            CornerRadius = info.UsesRadius ? 16 : 0,
        };
        e.X = MathF.Round((canvasW - e.Width) / 2);
        e.Y = MathF.Round((canvasH - e.Height) / 2);
        return e;
    }

    public Element Clone()
    {
        var e = (Element)MemberwiseClone();
        e.Fill = Fill.Clone();
        e.RemainFill = RemainFill.Clone();
        return e;
    }
}

public sealed class Theme
{
    public string Name { get; set; } = "Untitled";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 462;
    public FillSpec Background { get; set; } = FillSpec.Solid("#FF0C0E14");
    /// <summary>Optional image, GIF or video drawn over the background colour.</summary>
    public string BackgroundMedia { get; set; } = "";
    public MediaFit BackgroundMediaFit { get; set; } = MediaFit.Cover;
    public List<Element> Elements { get; set; } = new();

    [JsonIgnore] public bool IsVertical => Height > Width;

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static Theme FromJson(string json) => JsonSerializer.Deserialize<Theme>(json, Json) ?? new Theme();
    public Theme Clone() => FromJson(ToJson());

    /// <summary>Media files referenced by this theme (background and elements).</summary>
    public IEnumerable<string> MediaPaths()
    {
        if (!string.IsNullOrWhiteSpace(BackgroundMedia)) yield return BackgroundMedia;
        foreach (var e in Elements)
            if (e.IsMedia && !string.IsNullOrWhiteSpace(e.MediaPath)) yield return e.MediaPath;
    }

    /// <summary>
    /// Turns the whole layout by 90 degrees and swaps the canvas between horizontal and vertical.
    /// Every element moves with it and rotates too, so bars and charts change direction.
    /// </summary>
    public void RotateLayout(bool clockwise)
    {
        int oldW = Width, oldH = Height;
        foreach (var e in Elements)
        {
            var c = e.Center;
            // Clockwise: (x, y) -> (oldH - y, x). Counter-clockwise: (x, y) -> (y, oldW - x).
            float nx = clockwise ? oldH - c.Y : c.Y;
            float ny = clockwise ? c.X : oldW - c.X;
            e.X = MathF.Round(nx - e.Width / 2);
            e.Y = MathF.Round(ny - e.Height / 2);
            float rot = (e.Rotation + (clockwise ? 90 : -90)) % 360;
            e.Rotation = rot < 0 ? rot + 360 : rot;
        }
        Width = oldH;
        Height = oldW;
    }

    // ---------- built-in themes ----------

    const string TextColor = "#FFECF1FA", Dim = "#FF8C97AC", CardColor = "#FF181C28", Track = "#FF2E3548";

    internal static Element TextEl(string name, string text, float x, float y, float w, float h, float size,
        bool bold = false, string color = TextColor, HAlign align = HAlign.Left) => new()
    {
        Type = ElementType.Text, Name = name, Text = text, X = x, Y = y, Width = w, Height = h,
        FontSize = size, Bold = bold, Align = align, Fill = FillSpec.Solid(color),
    };

    internal static Element RectEl(string name, float x, float y, float w, float h, FillSpec fill, float radius,
        string bind = "") => new()
    {
        Type = ElementType.Rectangle, Name = name, X = x, Y = y, Width = w, Height = h,
        Fill = fill, CornerRadius = radius, Bind = bind,
    };

    internal static void AddCard(Theme t, float x, float top, float w, string title, string accent, string name,
        string temp, string load, (string label, string value)[] rows)
    {
        t.Elements.Add(RectEl(title + " card", x, top, w, 414, FillSpec.Solid(CardColor), 28));
        float l = x + 30, iw = w - 60;
        t.Elements.Add(TextEl(title + " title", title, l, top + 16, 200, 50, 40, true, accent));
        if (name != "") t.Elements.Add(TextEl(title + " name", name, l, top + 64, iw, 28, 18, color: Dim));
        if (temp != "") t.Elements.Add(TextEl(title + " temp", temp, l, top + 98, iw, 110, 96, true));
        t.Elements.Add(TextEl(title + " load label", "Load", l, top + 226, 150, 28, 22, color: Dim));
        t.Elements.Add(TextEl(title + " load value", load, l + iw - 180, top + 224, 180, 32, 26, true, align: HAlign.Right));
        t.Elements.Add(RectEl(title + " bar track", l, top + 260, iw, 16, FillSpec.Solid(Track), 8));
        string bind = load.Contains("ram") ? "ram.load" : load.Contains("gpu") ? "gpu.load" : "cpu.load";
        t.Elements.Add(RectEl(title + " bar", l, top + 260, iw, 16, new FillSpec
        {
            Type = FillType.Linear, Color1 = accent, Color2 = "#FFFFFFFF", ColorCount = 2, Angle = 0,
        }, 8, bind));
        float y = top + 296;
        foreach (var (label, value) in rows)
        {
            t.Elements.Add(TextEl(title + " " + label, label, l, y, 150, 28, 22, color: Dim));
            t.Elements.Add(TextEl(title + " " + label + " value", value, l + iw - 220, y - 2, 220, 32, 26, true, align: HAlign.Right));
            y += 36;
        }
    }

    internal static readonly (string, string)[] CpuRows = { ("Power", "{cpu.power} W"), ("Clock", "{cpu.clock} GHz"), ("Fan", "{cpu.fan} RPM") };
    internal static readonly (string, string)[] GpuRows = { ("Power", "{gpu.power} W"), ("Clock", "{gpu.clock} GHz"), ("VRAM", "{gpu.vram:0.0} / {gpu.vramtotal:0.0} GB") };
    internal static readonly (string, string)[] MemRows = { ("Used", "{ram.used:0.0} / {ram.total:0.0} GB"), ("Upload", "{net.up:0.0} Mbps"), ("Download", "{net.down:0.0} Mbps") };

    static FillSpec DarkBackground() => new()
    {
        Type = FillType.Linear, Color1 = "#FF0B0D14", Color2 = "#FF161B2C", ColorCount = 2, Angle = 90,
    };

    static FillSpec AccentGradient() => new()
    {
        Type = FillType.Linear, Color1 = "#FF5AAAFF", Color2 = "#FFAF87FF", Color3 = "#FFFF6EA8", ColorCount = 3,
    };

    /// <summary>Landscape dashboard for the 1920x462 bar display.</summary>
    public static Theme CreateDefault()
    {
        var t = new Theme { Name = "Default", Background = DarkBackground() };
        t.Elements.Add(TextEl("Time", "{time}", 40, 20, 520, 190, 150, true));
        t.Elements.Add(TextEl("Date", "{date}", 40, 215, 520, 50, 34, color: Dim));
        t.Elements.Add(TextEl("Host", "{host}", 40, 266, 520, 36, 24, color: Dim));
        t.Elements.Add(RectEl("Accent line", 40, 330, 480, 8, AccentGradient(), 4));
        AddCard(t, 580, 24, 420, "CPU", "#FF5AAAFF", "{cpu.name}", "{cpu.temp}{unit}", "{cpu.load} %", CpuRows);
        AddCard(t, 1020, 24, 420, "GPU", "#FFFF825A", "{gpu.name}", "{gpu.temp}{unit}", "{gpu.load} %", GpuRows);
        AddCard(t, 1460, 24, 436, "MEMORY", "#FFAF87FF", "", "", "{ram.load} %", MemRows);
        return t;
    }

    /// <summary>Portrait dashboard (462x1920) with the cards stacked.</summary>
    public static Theme CreateDefaultVertical()
    {
        var t = new Theme { Name = "Default Vertical", Width = 462, Height = 1920, Background = DarkBackground() };
        t.Elements.Add(TextEl("Time", "{time}", 24, 24, 414, 170, 130, true, align: HAlign.Center));
        t.Elements.Add(TextEl("Date", "{date}", 24, 198, 414, 44, 32, color: Dim, align: HAlign.Center));
        t.Elements.Add(TextEl("Host", "{host}", 24, 244, 414, 32, 22, color: Dim, align: HAlign.Center));
        t.Elements.Add(RectEl("Accent line", 91, 296, 280, 8, AccentGradient(), 4));
        AddCard(t, 24, 340, 414, "CPU", "#FF5AAAFF", "{cpu.name}", "{cpu.temp}{unit}", "{cpu.load} %", CpuRows);
        AddCard(t, 24, 800, 414, "GPU", "#FFFF825A", "{gpu.name}", "{gpu.temp}{unit}", "{gpu.load} %", GpuRows);
        AddCard(t, 24, 1260, 414, "MEMORY", "#FFAF87FF", "", "", "{ram.load} %", MemRows);
        return t;
    }
}

/// <summary>Themes stored as JSON files in %AppData%\HurricaneBeastPanel\themes.</summary>
public static class ThemeStore
{
    public static string Dir => Path.Combine(AppSettings.Dir, "themes");

    public static string SafeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim();
        return name.Length == 0 ? "Untitled" : name;
    }

    static string PathFor(string name) => Path.Combine(Dir, SafeName(name) + ".json");

    /// <summary>Makes sure the built-in themes exist (each is created once, so deleting one sticks).</summary>
    public static void EnsureDefault(AppSettings? settings = null)
    {
        Directory.CreateDirectory(Dir);
        if (!Directory.EnumerateFiles(Dir, "*.json").Any()) Save(Theme.CreateDefault());

        settings ??= AppSettings.Load();
        if (!settings.NeonThemeAdded)
        {
            if (!Exists("Neon Dashboard")) Save(NeonTheme.Create());
            settings.NeonThemeAdded = true;
            settings.Save();
        }
        if (settings.ExtraThemesVersion < 2)
        {
            foreach (var extra in ExtraThemes.All())
                if (!Exists(extra.Name)) Save(extra);
            settings.ExtraThemesVersion = 2;
            settings.Save();
        }
        if (!settings.VerticalThemeAdded)
        {
            if (!Exists("Default Vertical")) Save(Theme.CreateDefaultVertical());
            settings.VerticalThemeAdded = true;
            settings.Save();
        }
    }

    public static List<string> List()
    {
        EnsureDefault();
        return Directory.EnumerateFiles(Dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension).Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Theme Load(string name)
    {
        try
        {
            string path = PathFor(name);
            if (File.Exists(path))
            {
                var t = Theme.FromJson(File.ReadAllText(path));
                t.Name = SafeName(name);
                return t;
            }
        }
        catch (Exception ex) { Log.Write("Theme load failed: " + ex.Message); }
        return Theme.CreateDefault();
    }

    public static void Save(Theme theme)
    {
        Directory.CreateDirectory(Dir);
        theme.Name = SafeName(theme.Name);
        File.WriteAllText(PathFor(theme.Name), theme.ToJson());
    }

    public static bool Exists(string name) => File.Exists(PathFor(name));

    public static void Delete(string name)
    {
        try { File.Delete(PathFor(name)); } catch { }
    }
}
