using System.Drawing.Drawing2D;

namespace HurricaneBeastPanel;

/// <summary>Draws the gauges, graphs and meters of the widget library. Gauges are here, graphs and meters in the other partial files.</summary>
static partial class WidgetRenderer
{
    sealed class Ctx
    {
        public Graphics G = null!;
        public Element E = null!;
        public WidgetInfo Info = null!;
        /// <summary>Element bounds, already inset by half the border width.</summary>
        public RectangleF R;
        public SensorSnapshot Snap = null!;
        public bool Preview;
        public float F, F2, F3;
        public Color Main, Track, Accent;

        public Brush MainBrush(RectangleF r, bool inscribed = false) => E.Fill.CreateBrush(r, inscribed);

        public Brush TrackBrush(RectangleF r, bool inscribed = false) =>
            E.RemainEnabled ? E.RemainFill.CreateBrush(r, inscribed) : new SolidBrush(Color.Transparent);
    }

    static readonly Color Green = Color.FromArgb(255, 70, 210, 140);
    static readonly Color Amber = Color.FromArgb(255, 255, 190, 60);
    static readonly Color Red = Color.FromArgb(255, 255, 85, 85);

    public static void Draw(Graphics g, Element e, RectangleF rect, SensorSnapshot snap, bool preview)
    {
        var info = WidgetCatalog.Info(e.Style);
        float inset = e.BorderEnabled ? e.BorderWidth / 2 : 0;
        var c = new Ctx
        {
            G = g, E = e, Info = info, Snap = snap, Preview = preview,
            R = RectangleF.Inflate(rect, -inset, -inset),
            Main = ColorUtil.Parse(e.Fill.Color1),
            Track = ColorUtil.Parse(e.RemainFill.Color1),
            Accent = ColorUtil.Parse(e.AccentColor),
        };
        if (c.R.Width < 4 || c.R.Height < 4) return;
        c.F = Frac(c, e.Bind, 0.65f);
        c.F2 = Frac(c, e.Bind2, 0.4f);
        c.F3 = Frac(c, e.Bind3, 0.55f);

        switch (info.Category)
        {
            case WidgetCategory.Gauge: DrawGauge(c); break;
            case WidgetCategory.Graph: DrawGraph(c); break;
            default: DrawMeter(c); break;
        }
    }

    // ---------- values ----------

    /// <summary>Bound value as 0..1. An empty binding shows a demo value so the widget is visible while designing.</summary>
    static float Frac(Ctx c, string bind, float demo)
    {
        if (string.IsNullOrEmpty(bind)) return demo;
        float? v = Tokens.Number(bind, c.Snap);
        float span = c.E.BindMax - c.E.BindMin;
        return v.HasValue && span > 0 ? Math.Clamp((v.Value - c.E.BindMin) / span, 0f, 1f) : 0f;
    }

    /// <summary>Raw bound value (for digital displays); a demo value when unbound.</summary>
    static float? RawValue(Ctx c, string bind, float demo) =>
        string.IsNullOrEmpty(bind) ? demo : Tokens.Number(bind, c.Snap);

    static string Label(string bind)
    {
        if (string.IsNullOrEmpty(bind)) return "VALUE";
        string s = bind.ToUpperInvariant().Replace('.', ' ');
        return s.EndsWith(" LOAD") ? s[..^5] + " USAGE" : s;
    }

    // ---------- small helpers ----------

    static Color A(Color c, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), c.R, c.G, c.B);

    static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    static Color Zone(float f) => f < 0.6f ? Green : f < 0.85f ? Amber : Red;

    static PointF Center(RectangleF r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    static PointF Polar(PointF c, float radius, float deg)
    {
        double a = deg * Math.PI / 180;
        return new PointF((float)(c.X + radius * Math.Cos(a)), (float)(c.Y + radius * Math.Sin(a)));
    }

    static void Border(Ctx c, GraphicsPath path)
    {
        if (!c.E.BorderEnabled || c.E.BorderWidth <= 0) return;
        using var pen = new Pen(ColorUtil.Parse(c.E.BorderColor), c.E.BorderWidth) { LineJoin = LineJoin.Round };
        c.G.DrawPath(pen, path);
    }

    static void Dot(Graphics g, PointF p, float radius, Color color)
    {
        using var b = new SolidBrush(color);
        g.FillEllipse(b, p.X - radius, p.Y - radius, radius * 2, radius * 2);
    }

    static void Line(Graphics g, PointF a, PointF b, Color color, float width, bool round = false)
    {
        using var pen = new Pen(color, width);
        if (round) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; }
        g.DrawLine(pen, a, b);
    }

    static GraphicsPath RoundRectPath(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---------- arcs ----------

    /// <summary>Largest circle whose arc (start..start+sweep) fits in <paramref name="r"/>, so half and three-quarter gauges use the whole box.</summary>
    static RectangleF ArcFit(RectangleF r, float start, float sweep)
    {
        sweep = Math.Clamp(sweep, 1f, 360f);
        if (sweep >= 359.9f)
        {
            float s = Math.Min(r.Width, r.Height);
            return new RectangleF(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s);
        }
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        void Add(double deg)
        {
            double a = deg * Math.PI / 180, x = Math.Cos(a), y = Math.Sin(a);
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        Add(start);
        Add(start + sweep);
        for (int k = (int)Math.Ceiling(start / 90.0); k * 90.0 <= start + sweep; k++) Add(k * 90.0);
        double w = Math.Max(maxX - minX, 0.01), h = Math.Max(maxY - minY, 0.01);
        double scale = Math.Min(r.Width / w, r.Height / h);
        double cx = r.X + r.Width / 2 - (minX + maxX) / 2 * scale;
        double cy = r.Y + r.Height / 2 - (minY + maxY) / 2 * scale;
        float rad = (float)scale;
        return new RectangleF((float)cx - rad, (float)cy - rad, rad * 2, rad * 2);
    }

    static void Arc(Graphics g, RectangleF centerBox, float start, float sweep, Brush brush, float width, bool round)
    {
        if (centerBox.Width < 2 || centerBox.Height < 2 || width < 0.5f || Math.Abs(sweep) < 0.05f) return;
        using var pen = new Pen(brush, width)
        {
            StartCap = round ? LineCap.Round : LineCap.Flat,
            EndCap = round ? LineCap.Round : LineCap.Flat,
        };
        g.DrawArc(pen, centerBox, start, sweep);
    }

    static void GlowArc(Graphics g, RectangleF box, float start, float sweep, Color color, float width, bool round)
    {
        for (int i = 4; i >= 1; i--)
        {
            using var b = new SolidBrush(A(color, 12 + (5 - i) * 7));
            Arc(g, box, start, sweep, b, width + i * 5, round);
        }
    }

    /// <summary>Outer circle, centre-line box and thickness for an arc gauge.</summary>
    static (RectangleF outer, RectangleF box, float th) ArcBoxes(Ctx c, RectangleF? area = null)
    {
        float th = Math.Max(1, c.E.RingThickness);
        var outer = ArcFit(area ?? c.R, c.E.StartAngle, c.E.SweepAngle);
        th = Math.Min(th, outer.Width / 2 - 1);
        return (outer, RectangleF.Inflate(outer, -th / 2, -th / 2), th);
    }

    static void RingBorder(Ctx c, RectangleF outer, float th)
    {
        using var path = ThemeRenderer.RingSector(outer, RectangleF.Inflate(outer, -th, -th), c.E.StartAngle, Math.Clamp(c.E.SweepAngle, 1, 360));
        Border(c, path);
    }

    // ---------- gauges ----------

    static void DrawGauge(Ctx c)
    {
        switch (c.E.Style)
        {
            case WidgetStyle.HalfArc:
            case WidgetStyle.ThreeQuarterArc:
                ArcGauge(c, round: true, glow: false, dot: false); break;
            case WidgetStyle.FullRing: ArcGauge(c, round: false, glow: false, dot: false); break;
            case WidgetStyle.GlowingRing: ArcGauge(c, round: true, glow: true, dot: false); break;
            case WidgetStyle.ChunkyGlow: ArcGauge(c, round: false, glow: true, dot: false); break;
            case WidgetStyle.ThinArc: ArcGauge(c, round: true, glow: false, dot: true); break;
            case WidgetStyle.ProgressDot: ArcGauge(c, round: true, glow: true, dot: true); break;
            case WidgetStyle.GlassRing: ArcGauge(c, round: true, glow: false, dot: false, glass: true); break;
            case WidgetStyle.SegmentedRing: SegmentedGauge(c, round: false, thinScale: 1f, gapFraction: 0.3f); break;
            case WidgetStyle.DashedArc: SegmentedGauge(c, round: true, thinScale: 1f, gapFraction: 0.45f); break;
            case WidgetStyle.DualConcentric: DualConcentric(c); break;
            case WidgetStyle.NeedleSpeedometer: Speedometer(c); break;
            case WidgetStyle.PieSweep: PieSweep(c); break;
            case WidgetStyle.HudTechArc: HudArc(c); break;
            case WidgetStyle.VuMeter: VuMeter(c); break;
            case WidgetStyle.GearDial: GearDial(c); break;
            case WidgetStyle.SweepDisc: SweepDisc(c); break;
            case WidgetStyle.ZoneRing: ZoneRing(c); break;
            case WidgetStyle.RingTicks: RingTicks(c); break;
            case WidgetStyle.RingNeedle: RingNeedle(c); break;
            case WidgetStyle.DottedRing: DottedRing(c); break;
            case WidgetStyle.DigitalLcd: DigitalPanel(c, segmentedBar: false); break;
            case WidgetStyle.DigitalReadout: DigitalPanel(c, segmentedBar: true); break;
            case WidgetStyle.RadarHexagon: RadarHexagon(c); break;
        }
    }

    static void ArcGauge(Ctx c, bool round, bool glow, bool dot, bool glass = false)
    {
        var e = c.E;
        var (outer, box, th) = ArcBoxes(c);
        if (box.Width < 2) return;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f), vs = sweep * c.F;

        using (var tb = c.TrackBrush(outer, true))
            Arc(c.G, box, e.StartAngle, sweep, tb, th, round);

        if (glass)
        {
            // Glossy rim: a thin bright arc on the outer edge and a soft one on the inner edge.
            using (var rim = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                Arc(c.G, RectangleF.Inflate(box, th / 2 - 1.5f, th / 2 - 1.5f), e.StartAngle, sweep, rim, 3, false);
            using (var rim2 = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                Arc(c.G, RectangleF.Inflate(box, -th / 2 + 1.5f, -th / 2 + 1.5f), e.StartAngle, sweep, rim2, 3, false);
        }

        if (glow && vs > 0.5f) GlowArc(c.G, box, e.StartAngle, vs, c.Main, th, round);
        if (vs > 0.3f)
        {
            using var mb = c.MainBrush(outer, true);
            Arc(c.G, box, e.StartAngle, vs, mb, th, round);
        }
        if (dot)
        {
            var p = Polar(Center(box), box.Width / 2, e.StartAngle + vs);
            if (glow) Dot(c.G, p, th * 2.2f, A(c.Main, 40));
            Dot(c.G, p, Math.Max(th * 1.1f, 5), c.Main);
            Dot(c.G, p, Math.Max(th * 0.45f, 2.5f), Color.FromArgb(235, 255, 255, 255));
        }
        RingBorder(c, outer, th);
    }

    static void SegmentedGauge(Ctx c, bool round, float thinScale, float gapFraction)
    {
        var e = c.E;
        var (outer, box, th) = ArcBoxes(c);
        if (box.Width < 2) return;
        th *= thinScale;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        int count = Math.Clamp(e.Segments, 2, 200);
        float step = sweep / count, gap = step * gapFraction;
        using var mb = c.MainBrush(outer, true);
        using var tb = c.TrackBrush(outer, true);
        for (int i = 0; i < count; i++)
        {
            bool lit = (i + 0.5f) / count <= c.F;
            Arc(c.G, box, e.StartAngle + i * step + gap / 2, Math.Max(step - gap, 0.5f), lit ? mb : tb, th, round);
        }
        RingBorder(c, outer, th);
    }

    static void DualConcentric(Ctx c)
    {
        var e = c.E;
        var (outer, box, th) = ArcBoxes(c);
        if (box.Width < 2) return;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        float gap = th * 0.4f, th2 = th * 0.75f;
        var inner = RectangleF.Inflate(outer, -(th + gap), -(th + gap));
        var box2 = RectangleF.Inflate(inner, -th2 / 2, -th2 / 2);

        using (var tb = c.TrackBrush(outer, true))
        {
            Arc(c.G, box, e.StartAngle, sweep, tb, th, true);
            Arc(c.G, box2, e.StartAngle, sweep, tb, th2, true);
        }
        using (var mb = c.MainBrush(outer, true))
            Arc(c.G, box, e.StartAngle, sweep * c.F, mb, th, true);
        using (var ab = new SolidBrush(c.Accent))
            Arc(c.G, box2, e.StartAngle, sweep * c.F2, ab, th2, true);
        RingBorder(c, outer, th);
    }

    static void Speedometer(Ctx c)
    {
        var e = c.E;
        var box = ArcFit(c.R, e.StartAngle, e.SweepAngle);
        var ctr = Center(box);
        float r = box.Width / 2, sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        int n = Math.Clamp(e.Segments, 5, 120);
        float tickScale = Math.Max(0.6f, r / 130f);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, ang = e.StartAngle + sweep * t;
            bool major = i % 5 == 0;
            float len = r * (major ? 0.17f : 0.09f);
            Color col = t <= c.F + 1e-4f ? (t > 0.8f ? c.Accent : c.Main) : A(c.Track.A == 0 ? c.Main : Mix(c.Track, c.Main, 0.35f), 160);
            if (t > 0.8f && t > c.F) col = A(c.Accent, 120);
            Line(c.G, Polar(ctr, r, ang), Polar(ctr, r - len, ang), col, (major ? 3f : 1.6f) * tickScale);
        }
        float needleAng = e.StartAngle + sweep * c.F;
        Line(c.G, ctr, Polar(ctr, r * 0.78f, needleAng), c.Main, 3.2f * tickScale, round: true);
        Dot(c.G, ctr, r * 0.08f, c.Main);
        Dot(c.G, ctr, r * 0.035f, Color.FromArgb(255, 10, 14, 20));
        using var path = new GraphicsPath();
        if (e.BorderEnabled) { path.AddEllipse(box); Border(c, path); }
    }

    static void PieSweep(Ctx c)
    {
        var e = c.E;
        float s = Math.Min(c.R.Width, c.R.Height);
        var box = new RectangleF(c.R.X + (c.R.Width - s) / 2, c.R.Y + (c.R.Height - s) / 2, s, s);
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f), vs = sweep * c.F;
        if (e.RemainEnabled && vs < sweep - 0.05f)
        {
            using var path = ThemeRenderer.PieSector(box, e.StartAngle + vs, sweep - vs);
            using var tb = c.TrackBrush(box, true);
            c.G.FillPath(tb, path);
        }
        if (vs > 0.05f)
        {
            using var path = ThemeRenderer.PieSector(box, e.StartAngle, vs);
            using var mb = c.MainBrush(box, true);
            c.G.FillPath(mb, path);
        }
        using var full = ThemeRenderer.PieSector(box, e.StartAngle, sweep);
        Border(c, full);
    }

    static void HudArc(Ctx c)
    {
        var e = c.E;
        var inner = RectangleF.Inflate(c.R, -c.R.Width * 0.12f, -c.R.Height * 0.12f);
        var (outer, box, th) = ArcBoxes(c, inner);
        if (box.Width < 2) return;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);

        // Corner brackets around the whole widget.
        float len = Math.Min(c.R.Width, c.R.Height) * 0.14f;
        using (var pen = new Pen(A(c.Main, 150), 2f))
        {
            var r = c.R;
            c.G.DrawLines(pen, new[] { new PointF(r.Left, r.Top + len), new PointF(r.Left, r.Top), new PointF(r.Left + len, r.Top) });
            c.G.DrawLines(pen, new[] { new PointF(r.Right - len, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Top + len) });
            c.G.DrawLines(pen, new[] { new PointF(r.Left, r.Bottom - len), new PointF(r.Left, r.Bottom), new PointF(r.Left + len, r.Bottom) });
            c.G.DrawLines(pen, new[] { new PointF(r.Right - len, r.Bottom), new PointF(r.Right, r.Bottom), new PointF(r.Right, r.Bottom - len) });
        }

        using (var tb = c.TrackBrush(outer, true)) Arc(c.G, box, e.StartAngle, sweep, tb, th, false);
        using (var mb = c.MainBrush(outer, true)) Arc(c.G, box, e.StartAngle, sweep * c.F, mb, th, false);

        // Small tick marks inside the ring at start, middle and end.
        var ctr = Center(box);
        float innerR = box.Width / 2 - th / 2;
        foreach (float t in new[] { 0f, 0.5f, 1f })
        {
            float ang = e.StartAngle + sweep * t;
            Line(c.G, Polar(ctr, innerR - 4, ang), Polar(ctr, innerR - 4 - th * 0.7f, ang), A(c.Accent, 200), 2f);
        }
        RingBorder(c, outer, th);
    }

    static void VuMeter(Ctx c)
    {
        var e = c.E;
        var r = c.R;
        using (var panel = RoundRectPath(r, Math.Min(r.Width, r.Height) * 0.06f))
        {
            using (var tb = c.TrackBrush(r)) c.G.FillPath(tb, panel);
            if (!e.BorderEnabled)
                using (var pen = new Pen(A(c.Main, 70), 1.5f)) c.G.DrawPath(pen, panel);
            Border(c, panel);
        }

        var pivot = new PointF(r.X + r.Width / 2, r.Bottom - r.Height * 0.1f);
        float radius = Math.Min(r.Width * 0.46f, r.Height * 0.82f);
        float start = e.StartAngle, sweep = Math.Clamp(e.SweepAngle, 10f, 180f);
        var arcBox = new RectangleF(pivot.X - radius, pivot.Y - radius, radius * 2, radius * 2);
        using (var b = new SolidBrush(A(c.Main, 170))) Arc(c.G, arcBox, start, sweep * 0.78f, b, 3, false);
        using (var b = new SolidBrush(A(c.Accent, 220))) Arc(c.G, arcBox, start + sweep * 0.78f, sweep * 0.22f, b, 3, false);

        int n = Math.Clamp(e.Segments, 3, 40);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, ang = start + sweep * t;
            Line(c.G, Polar(pivot, radius, ang), Polar(pivot, radius - radius * (i % 5 == 0 ? 0.1f : 0.06f), ang),
                t > 0.78f ? c.Accent : A(c.Main, 190), 1.6f);
        }
        float needleAng = start + sweep * c.F;
        Line(c.G, pivot, Polar(pivot, radius * 0.94f, needleAng), Color.FromArgb(240, 240, 244, 250), 2.4f, round: true);
        Dot(c.G, pivot, radius * 0.05f, c.Main);
    }

    static void GearDial(Ctx c)
    {
        var e = c.E;
        float s = Math.Min(c.R.Width, c.R.Height);
        var outer = new RectangleF(c.R.X + (c.R.Width - s) / 2, c.R.Y + (c.R.Height - s) / 2, s, s);
        float th = Math.Clamp(e.RingThickness, 2, s / 2 - 2);
        var box = RectangleF.Inflate(outer, -th / 2, -th / 2);
        int count = Math.Clamp(e.Segments, 6, 120);
        float step = 360f / count, gap = step * 0.3f;
        using var mb = c.MainBrush(outer, true);
        using var tb = c.TrackBrush(outer, true);
        for (int i = 0; i < count; i++)
        {
            bool lit = (i + 0.5f) / count <= c.F;
            Arc(c.G, box, -90 + i * step + gap / 2, step - gap, lit ? mb : tb, th, false);
        }
        var ctr = Center(outer);
        float r = outer.Width / 2;
        using (var pen = new Pen(A(c.Main, 120), 2f))
            c.G.DrawEllipse(pen, ctr.X - r * 0.62f, ctr.Y - r * 0.62f, r * 1.24f, r * 1.24f);
        for (int i = 0; i < 8; i++)
            Dot(c.G, Polar(ctr, r * 0.42f, i * 45f), Math.Max(2f, r * 0.02f), A(c.Main, 160));
        using var path = new GraphicsPath();
        if (e.BorderEnabled) { path.AddEllipse(outer); Border(c, path); }
    }

    static void SweepDisc(Ctx c)
    {
        var e = c.E;
        float s = Math.Min(c.R.Width, c.R.Height);
        var box = new RectangleF(c.R.X + (c.R.Width - s) / 2, c.R.Y + (c.R.Height - s) / 2, s, s);
        var ctr = Center(box);
        float r = s / 2;
        using (var tb = c.TrackBrush(box, true)) c.G.FillEllipse(tb, box);
        using (var pen = new Pen(A(c.Main, 55), 1.5f))
        {
            foreach (float k in new[] { 0.33f, 0.66f, 1f }) c.G.DrawEllipse(pen, ctr.X - r * k, ctr.Y - r * k, r * 2 * k, r * 2 * k);
            c.G.DrawLine(pen, ctr.X - r, ctr.Y, ctr.X + r, ctr.Y);
            c.G.DrawLine(pen, ctr.X, ctr.Y - r, ctr.X, ctr.Y + r);
        }
        float sweep = 360f * c.F;
        if (sweep > 0.5f)
        {
            using var path = ThemeRenderer.PieSector(box, e.StartAngle, sweep);
            using var b = new SolidBrush(A(c.Main, 95));
            c.G.FillPath(b, path);
            Line(c.G, ctr, Polar(ctr, r, e.StartAngle + sweep), c.Main, 3f);
        }
        using var edge = new GraphicsPath();
        if (e.BorderEnabled) { edge.AddEllipse(box); Border(c, edge); }
    }

    static void ZoneRing(Ctx c)
    {
        var e = c.E;
        var (outer, box, th) = ArcBoxes(c);
        if (box.Width < 2) return;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        using (var g1 = new SolidBrush(A(Green, 70))) Arc(c.G, box, e.StartAngle, sweep * 0.6f, g1, th, false);
        using (var g2 = new SolidBrush(A(Amber, 70))) Arc(c.G, box, e.StartAngle + sweep * 0.6f, sweep * 0.25f, g2, th, false);
        using (var g3 = new SolidBrush(A(Red, 70))) Arc(c.G, box, e.StartAngle + sweep * 0.85f, sweep * 0.15f, g3, th, false);
        using (var vb = new SolidBrush(Zone(c.F))) Arc(c.G, box, e.StartAngle, sweep * c.F, vb, th, false);
        RingBorder(c, outer, th);
    }

    static void RingTicks(Ctx c)
    {
        var e = c.E;
        float tick = Math.Min(c.R.Width, c.R.Height) * 0.07f;
        var area = RectangleF.Inflate(c.R, -tick - 3, -tick - 3);
        var (outer, box, th) = ArcBoxes(c, area);
        if (box.Width < 2) return;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        using (var tb = c.TrackBrush(outer, true)) Arc(c.G, box, e.StartAngle, sweep, tb, th, false);
        using (var mb = c.MainBrush(outer, true)) Arc(c.G, box, e.StartAngle, sweep * c.F, mb, th, false);

        var ctr = Center(outer);
        float r = outer.Width / 2 + 4;
        int n = Math.Clamp(e.Segments, 4, 180);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            bool lit = t <= c.F + 1e-4f;
            float len = tick * (i % 4 == 0 ? 1f : 0.55f);
            Line(c.G, Polar(ctr, r, e.StartAngle + sweep * t), Polar(ctr, r + len, e.StartAngle + sweep * t),
                lit ? A(c.Main, 220) : A(c.Main, 60), 1.8f);
        }
        RingBorder(c, outer, th);
    }

    static void RingNeedle(Ctx c)
    {
        var e = c.E;
        var (outer, box, th) = ArcBoxes(c);
        if (box.Width < 2) return;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        using (var tb = c.TrackBrush(outer, true)) Arc(c.G, box, e.StartAngle, sweep, tb, th, true);
        using (var mb = c.MainBrush(outer, true)) Arc(c.G, box, e.StartAngle, sweep * c.F, mb, th, true);

        var ctr = Center(box);
        float r = box.Width / 2 - th / 2;
        using (var pen = new Pen(A(c.Main, 60), 1.5f))
            c.G.DrawEllipse(pen, ctr.X - r * 0.8f, ctr.Y - r * 0.8f, r * 1.6f, r * 1.6f);
        float ang = e.StartAngle + sweep * c.F;
        Line(c.G, ctr, Polar(ctr, r * 0.72f, ang), Color.FromArgb(235, 240, 244, 250), 3f, round: true);
        Dot(c.G, ctr, r * 0.07f, c.Accent);
        RingBorder(c, outer, th);
    }

    static void DottedRing(Ctx c)
    {
        var e = c.E;
        var (outer, box, th) = ArcBoxes(c);
        if (box.Width < 2) return;
        var ctr = Center(box);
        float r = box.Width / 2, sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        int n = Math.Clamp(e.Segments, 4, 240);
        bool full = sweep >= 359.9f;
        for (int i = 0; i < n; i++)
        {
            float t = full ? i / (float)n : i / (float)(n - 1);
            bool lit = t <= c.F + 1e-4f;
            float size = lit ? th * (0.28f + 0.2f * t) : th * 0.2f;
            Dot(c.G, Polar(ctr, r, e.StartAngle + sweep * t), Math.Max(size, 1.2f), lit ? c.Main : A(c.Main, 55));
        }
        using var path = new GraphicsPath();
        if (e.BorderEnabled) { path.AddEllipse(outer); Border(c, path); }
    }

    static void RadarHexagon(Ctx c)
    {
        var e = c.E;
        float labelSize = Math.Max(9f, Math.Min(c.R.Width, c.R.Height) * 0.055f);
        var area = RectangleF.Inflate(c.R, -labelSize * 3.4f, -labelSize * 1.9f);
        float r = Math.Min(area.Width, area.Height) / 2;
        if (r < 8) return;
        var ctr = Center(area);
        var corners = Enumerable.Range(0, 6).Select(i => Polar(ctr, r, -90 + i * 60)).ToArray();

        using (var pen = new Pen(A(c.Main, 70), 1.5f))
        {
            foreach (float k in new[] { 0.34f, 0.67f, 1f })
                c.G.DrawPolygon(pen, corners.Select(p => new PointF(ctr.X + (p.X - ctr.X) * k, ctr.Y + (p.Y - ctr.Y) * k)).ToArray());
            for (int i = 0; i < 6; i += 2) c.G.DrawLine(pen, ctr, corners[i]);
        }

        // Three inputs on every second corner.
        var values = new[] { c.F, c.F2, c.F3 };
        var binds = new[] { e.Bind, e.Bind2, e.Bind3 };
        var pts = new PointF[3];
        for (int i = 0; i < 3; i++)
        {
            var corner = corners[i * 2];
            float v = Math.Max(values[i], 0.02f);
            pts[i] = new PointF(ctr.X + (corner.X - ctr.X) * v, ctr.Y + (corner.Y - ctr.Y) * v);
        }
        using (var fill = new SolidBrush(A(c.Main, 70))) c.G.FillPolygon(fill, pts);
        using (var pen = new Pen(c.Main, 2.5f) { LineJoin = LineJoin.Round }) c.G.DrawPolygon(pen, pts);
        foreach (var p in pts) Dot(c.G, p, 3.5f, c.Main);

        using var font = new Font("Segoe UI", labelSize, GraphicsUnit.Pixel);
        using var text = new SolidBrush(A(c.Main, 190));
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        for (int i = 0; i < 3; i++)
        {
            var corner = corners[i * 2];
            float dx = corner.X - ctr.X, dy = corner.Y - ctr.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            var lp = new PointF(corner.X + dx / len * labelSize * 1.5f, corner.Y + dy / len * labelSize * 1.4f);
            c.G.DrawString(Label(binds[i]), font, text, lp, fmt);
        }
        using var path = new GraphicsPath();
        if (e.BorderEnabled) { path.AddPolygon(corners); Border(c, path); }
    }

    // ---------- digital displays ----------

    static readonly byte[] SegMask = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

    /// <summary>One seven-segment digit; digit &lt; 0 draws only the dim "ghost" segments.</summary>
    static void SevenSeg(Graphics g, int digit, RectangleF cell, Color on, Color off)
    {
        float w = cell.Width, h = cell.Height;
        float t = Math.Min(w * 0.2f, h * 0.11f), gap = t * 0.2f, half = h / 2 - t / 2 - 2 * gap;
        float x0 = cell.X, y0 = cell.Y;
        var rects = new (RectangleF r, bool horizontal, int bit)[]
        {
            (new RectangleF(x0 + t / 2 + gap, y0, w - t - 2 * gap, t), true, 1),            // a
            (new RectangleF(x0 + w - t, y0 + t / 2 + gap, t, half), false, 2),               // b
            (new RectangleF(x0 + w - t, y0 + h / 2 + gap, t, half), false, 4),               // c
            (new RectangleF(x0 + t / 2 + gap, y0 + h - t, w - t - 2 * gap, t), true, 8),     // d
            (new RectangleF(x0, y0 + h / 2 + gap, t, half), false, 16),                      // e
            (new RectangleF(x0, y0 + t / 2 + gap, t, half), false, 32),                      // f
            (new RectangleF(x0 + t / 2 + gap, y0 + h / 2 - t / 2, w - t - 2 * gap, t), true, 64), // g
        };
        int mask = digit >= 0 && digit <= 9 ? SegMask[digit] : 0;
        foreach (var (r, horizontal, bit) in rects)
        {
            var pts = horizontal
                ? new[]
                {
                    new PointF(r.X, r.Y + r.Height / 2), new PointF(r.X + r.Height / 2, r.Y), new PointF(r.Right - r.Height / 2, r.Y),
                    new PointF(r.Right, r.Y + r.Height / 2), new PointF(r.Right - r.Height / 2, r.Bottom), new PointF(r.X + r.Height / 2, r.Bottom),
                }
                : new[]
                {
                    new PointF(r.X + r.Width / 2, r.Y), new PointF(r.Right, r.Y + r.Width / 2), new PointF(r.Right, r.Bottom - r.Width / 2),
                    new PointF(r.X + r.Width / 2, r.Bottom), new PointF(r.X, r.Bottom - r.Width / 2), new PointF(r.X, r.Y + r.Width / 2),
                };
            using var b = new SolidBrush((mask & bit) != 0 ? on : off);
            g.FillPolygon(b, pts);
        }
    }

    static void DigitalPanel(Ctx c, bool segmentedBar)
    {
        var e = c.E;
        var r = c.R;
        float radius = Math.Min(r.Width, r.Height) * 0.08f;
        using (var panel = RoundRectPath(r, radius))
        {
            using (var tb = c.TrackBrush(r)) c.G.FillPath(tb, panel);
            if (!e.BorderEnabled)
                using (var pen = new Pen(A(c.Main, 60), 1.5f)) c.G.DrawPath(pen, panel);
            Border(c, panel);
        }

        float? raw = RawValue(c, e.Bind, 65);
        int value = raw.HasValue ? (int)Math.Clamp(Math.Round(raw.Value), 0, 999) : 0;
        string digits = value.ToString("000");
        bool leadingBlank = value < 100;
        bool tensBlank = value < 10;

        float pad = Math.Min(r.Width, r.Height) * 0.1f;
        float barH = segmentedBar ? r.Height * 0.09f : r.Height * 0.07f;
        float digitsH = r.Height - pad * 2 - barH - pad * 0.7f;
        float cellW = Math.Min((r.Width - pad * 2) / 3.6f, digitsH * 0.6f);
        float gapX = cellW * 0.3f;
        float totalW = cellW * 3 + gapX * 2;
        float x = r.X + (r.Width - totalW) / 2, y = r.Y + pad;
        Color off = A(c.Main, 28);
        for (int i = 0; i < 3; i++)
        {
            var cell = new RectangleF(x + i * (cellW + gapX), y, cellW, digitsH);
            SevenSeg(c.G, -1, cell, off, off);
            bool blank = (i == 0 && leadingBlank) || (i == 1 && tensBlank);
            if (!blank) SevenSeg(c.G, digits[i] - '0', cell, c.Main, off);
        }

        var bar = new RectangleF(r.X + pad, r.Bottom - pad - barH, r.Width - pad * 2, barH);
        if (segmentedBar)
        {
            int n = Math.Clamp(e.Segments, 4, 40);
            float segW = (bar.Width - (n - 1) * 3) / n;
            for (int i = 0; i < n; i++)
            {
                bool lit = (i + 0.5f) / n <= c.F;
                using var b = new SolidBrush(lit ? c.Main : off);
                c.G.FillRectangle(b, bar.X + i * (segW + 3), bar.Y, segW, bar.Height);
            }
        }
        else
        {
            using var trackPath = RoundRectPath(bar, bar.Height / 2);
            using (var tb = new SolidBrush(off)) c.G.FillPath(tb, trackPath);
            var fillRect = new RectangleF(bar.X, bar.Y, bar.Width * c.F, bar.Height);
            if (fillRect.Width > 1)
            {
                var state = c.G.Save();
                c.G.SetClip(trackPath, CombineMode.Intersect);
                using var mb = c.MainBrush(bar);
                c.G.FillRectangle(mb, fillRect);
                c.G.Restore(state);
            }
        }
    }
}
