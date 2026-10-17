namespace HurricaneBeastPanel;

public enum WidgetCategory { Gauge, Graph, Meter }

public enum WidgetStyle
{
    // Gauges
    HalfArc, ThreeQuarterArc, FullRing, SegmentedRing, DualConcentric, NeedleSpeedometer, GlowingRing, PieSweep,
    DashedArc, HudTechArc, VuMeter, GearDial, ThinArc, SweepDisc, ZoneRing, RingTicks, RingNeedle, DottedRing,
    ChunkyGlow, DigitalLcd, RadarHexagon, GlassRing, ProgressDot, DigitalReadout,

    // Graphs (draw the recent history of a value)
    SimpleLine, FilledArea, SteppedLine, SmoothSpline, DualLine, DottedPoints, GradientFill, BarSparkline,
    GlowingLine, Oscilloscope, SparklineArea, SoftFill, PillBars, PixelMatrix,

    // Meters (a level that fills up)
    TankLevel, LedArray, Thermometer, Battery, SignalStrength, WaveFill, Cylinder3D, DiamondMeter,
    StackedBars, SolidMeter, SegmentedMeter, GradientMeter, NeonGlowMeter, LcdBlocks, ThinRailDot, DualComparison,
    SlimBar, ZoneBar, GradientGlow, StripedFill, DualSided, FlatFill, Capsule, NotchBar, RoundedPill, NeonOutline,
}

/// <summary>Defaults and which settings apply to a widget style (drives the editor panel and the add menus).</summary>
public sealed class WidgetInfo
{
    public WidgetStyle Style;
    public WidgetCategory Category;
    public string Name = "";
    public float Width = 260, Height = 260;
    public float Start = 135, Sweep = 270, Thickness = 24;
    public int Segments = 12, Samples = 60;
    public bool Bind2, Bind3;
    public bool UsesSegments, UsesRing, UsesAngles, UsesRadius, UsesSamples, UsesAccent, UsesLineWidth;
    /// <summary>Meters normally turn to run left-to-right when the element is wider than tall. Liquid and symbol styles never do.</summary>
    public bool Rotates;
    public FillSpec? Fill;
    public string Track = "#FF063A3C";
    public string Accent = "#FFFF4D4D";
}

public static class WidgetCatalog
{
    const string Cyan = "#FF00D2D2";

    public static readonly IReadOnlyList<WidgetInfo> All = Build();

    static readonly Dictionary<WidgetStyle, WidgetInfo> ByStyle = All.ToDictionary(i => i.Style);

    public static WidgetInfo Info(WidgetStyle style) => ByStyle[style];

    public static IEnumerable<WidgetInfo> In(WidgetCategory category) => All.Where(i => i.Category == category);

    static FillSpec Solid(string c) => FillSpec.Solid(c);

    static FillSpec OrangeToGreen() => new()
    {
        Type = FillType.Linear, Color1 = "#FFFF9F1C", Color2 = "#FF2BF28A", ColorCount = 2, Angle = 90,
    };

    static List<WidgetInfo> Build()
    {
        var list = new List<WidgetInfo>();

        void Gauge(WidgetStyle s, string name, float w, float h, float start, float sweep, float th,
            int segs = 12, bool ring = true, bool angles = true, bool accent = false, bool seg = false, bool b2 = false, bool b3 = false)
            => list.Add(new WidgetInfo
            {
                Style = s, Category = WidgetCategory.Gauge, Name = name, Width = w, Height = h, Start = start, Sweep = sweep,
                Thickness = th, Segments = segs, UsesRing = ring, UsesAngles = angles, UsesAccent = accent, UsesSegments = seg,
                Bind2 = b2, Bind3 = b3, Fill = Solid(Cyan),
            });

        Gauge(WidgetStyle.HalfArc, "Half-arc (180°)", 300, 170, 180, 180, 22);
        Gauge(WidgetStyle.ThreeQuarterArc, "Three-quarter arc", 260, 260, 135, 270, 24);
        Gauge(WidgetStyle.FullRing, "Full ring (360°)", 260, 260, -90, 360, 22);
        Gauge(WidgetStyle.SegmentedRing, "Segmented ring", 260, 260, -90, 360, 28, segs: 36, seg: true);
        Gauge(WidgetStyle.DualConcentric, "Dual concentric", 260, 260, 135, 270, 22, accent: true, b2: true);
        Gauge(WidgetStyle.NeedleSpeedometer, "Needle / speedometer", 280, 280, 135, 270, 24, segs: 36, ring: false, accent: true, seg: true);
        Gauge(WidgetStyle.GlowingRing, "Glowing ring", 260, 260, -90, 360, 18);
        Gauge(WidgetStyle.PieSweep, "Pie sweep", 260, 260, -90, 360, 0, ring: false);
        Gauge(WidgetStyle.DashedArc, "Dashed arc", 260, 260, 135, 270, 14, segs: 24, seg: true);
        Gauge(WidgetStyle.HudTechArc, "HUD tech arc", 280, 280, 135, 270, 22, accent: true);
        Gauge(WidgetStyle.VuMeter, "VU meter", 320, 200, 210, 120, 0, segs: 11, ring: false, accent: true, seg: true);
        Gauge(WidgetStyle.GearDial, "Gear dial", 260, 260, -90, 360, 30, segs: 36, seg: true, angles: false);
        Gauge(WidgetStyle.ThinArc, "Thin arc", 260, 260, 135, 270, 6);
        Gauge(WidgetStyle.SweepDisc, "Sweep disc", 260, 260, -90, 360, 0, ring: false, angles: false);
        Gauge(WidgetStyle.ZoneRing, "Zone ring", 260, 260, 135, 270, 26);
        Gauge(WidgetStyle.RingTicks, "Full ring + ticks", 260, 260, 135, 270, 20, segs: 48, seg: true);
        Gauge(WidgetStyle.RingNeedle, "Ring + needle", 260, 260, 135, 270, 20, accent: true);
        Gauge(WidgetStyle.DottedRing, "Dotted ring", 260, 260, -90, 360, 16, segs: 60, seg: true);
        Gauge(WidgetStyle.ChunkyGlow, "Chunky glow", 260, 260, -90, 360, 60);
        Gauge(WidgetStyle.DigitalLcd, "Digital LCD", 320, 190, 0, 0, 0, ring: false, angles: false);
        Gauge(WidgetStyle.RadarHexagon, "Radar hexagon", 320, 280, 0, 0, 0, ring: false, angles: false, b2: true, b3: true);
        Gauge(WidgetStyle.GlassRing, "Glass ring", 260, 260, 135, 270, 36);
        Gauge(WidgetStyle.ProgressDot, "Progress dot", 260, 260, -90, 360, 8);
        Gauge(WidgetStyle.DigitalReadout, "Digital readout", 320, 180, 0, 0, 0, segs: 12, ring: false, angles: false, seg: true);

        void Graph(WidgetStyle s, string name, bool accent = false, FillSpec? fill = null, string? track = null,
            float w = 420, float h = 180, bool b2 = false, bool seg = false, bool samples = true, bool lineWidth = true)
            => list.Add(new WidgetInfo
            {
                Style = s, Category = WidgetCategory.Graph, Name = name, Width = w, Height = h, Samples = 60,
                UsesSamples = samples, UsesLineWidth = lineWidth, UsesAccent = accent, Bind2 = b2, UsesSegments = seg,
                Fill = fill ?? Solid(Cyan), Track = track ?? "#FF063A3C", Segments = 12,
            });

        Graph(WidgetStyle.SimpleLine, "Simple line");
        Graph(WidgetStyle.FilledArea, "Filled area");
        Graph(WidgetStyle.SteppedLine, "Stepped / staircase");
        Graph(WidgetStyle.SmoothSpline, "Smooth spline");
        Graph(WidgetStyle.DualLine, "Dual line (shadow)", b2: true);
        Graph(WidgetStyle.DottedPoints, "Dotted points");
        Graph(WidgetStyle.GradientFill, "Gradient fill");
        Graph(WidgetStyle.BarSparkline, "Bar sparkline", lineWidth: false);
        Graph(WidgetStyle.GlowingLine, "Glowing line");
        Graph(WidgetStyle.Oscilloscope, "Oscilloscope", fill: Solid("#FF3CDC78"), track: "#FF07140C");
        Graph(WidgetStyle.SparklineArea, "Sparkline area");
        Graph(WidgetStyle.SoftFill, "Soft fill");
        Graph(WidgetStyle.PillBars, "Pill bar", lineWidth: false);
        Graph(WidgetStyle.PixelMatrix, "Pixel matrix", w: 300, h: 240, seg: true, samples: false, lineWidth: false);
        list.Last().Segments = 12;

        void Meter(WidgetStyle s, string name, float w, float h, bool rotates = true, bool seg = false, int segs = 12,
            bool b2 = false, bool b3 = false, bool accent = false, bool radius = false, FillSpec? fill = null, string? track = null,
            string? accentColor = null)
            => list.Add(new WidgetInfo
            {
                Style = s, Category = WidgetCategory.Meter, Name = name, Width = w, Height = h, Rotates = rotates,
                UsesSegments = seg, Segments = segs, Bind2 = b2, Bind3 = b3, UsesAccent = accent, UsesRadius = radius,
                Fill = fill ?? Solid(Cyan), Track = track ?? "#FF063A3C", Accent = accentColor ?? "#FFFF4D4D",
            });

        Meter(WidgetStyle.TankLevel, "Tank level", 300, 190, rotates: false, radius: true);
        Meter(WidgetStyle.LedArray, "LED array (VU meter)", 80, 300, seg: true, segs: 12, accent: true, fill: Solid("#FF2BF28A"));
        Meter(WidgetStyle.Thermometer, "Thermometer", 100, 300, rotates: false);
        Meter(WidgetStyle.Battery, "Battery level", 120, 210, rotates: false, seg: true, segs: 10, accent: true, fill: Solid("#FF2BF28A"));
        Meter(WidgetStyle.SignalStrength, "Signal strength", 220, 180, rotates: false, seg: true, segs: 5);
        Meter(WidgetStyle.WaveFill, "Wave fill meter", 300, 190, rotates: false, radius: true);
        Meter(WidgetStyle.Cylinder3D, "3D cylinder", 180, 250, rotates: false);
        Meter(WidgetStyle.DiamondMeter, "Diamond meter", 220, 250, rotates: false);
        Meter(WidgetStyle.StackedBars, "Stacked bars", 300, 130, rotates: false, b2: true, b3: true, radius: true);
        Meter(WidgetStyle.SolidMeter, "Solid", 90, 260);
        Meter(WidgetStyle.SegmentedMeter, "Segmented", 110, 260, seg: true, segs: 14);
        Meter(WidgetStyle.GradientMeter, "Gradient", 100, 260, fill: OrangeToGreen());
        Meter(WidgetStyle.NeonGlowMeter, "Neon glow", 70, 280);
        Meter(WidgetStyle.LcdBlocks, "LCD blocks", 120, 260, seg: true, segs: 10, fill: Solid("#FF3DFF7A"), track: "#FF0B2A14");
        Meter(WidgetStyle.ThinRailDot, "Thin rail + dot", 60, 280);
        Meter(WidgetStyle.DualComparison, "Dual comparison", 150, 260, b2: true, accent: true, accentColor: "#FF0A8F8F");
        Meter(WidgetStyle.SlimBar, "Slim bar", 40, 280);
        Meter(WidgetStyle.ZoneBar, "Zone bar", 110, 260);
        Meter(WidgetStyle.GradientGlow, "Gradient glow", 80, 280, fill: OrangeToGreen());
        Meter(WidgetStyle.StripedFill, "Striped fill", 110, 260);
        Meter(WidgetStyle.DualSided, "Dual-sided", 110, 260, b2: true, accent: true);
        Meter(WidgetStyle.FlatFill, "Simple flat fill", 70, 260);
        Meter(WidgetStyle.Capsule, "Capsule", 100, 280);
        Meter(WidgetStyle.NotchBar, "Notch bar", 100, 280, seg: true, segs: 12);
        Meter(WidgetStyle.RoundedPill, "Rounded pill", 110, 280);
        Meter(WidgetStyle.NeonOutline, "Neon outline", 110, 260);

        return list;
    }
}
