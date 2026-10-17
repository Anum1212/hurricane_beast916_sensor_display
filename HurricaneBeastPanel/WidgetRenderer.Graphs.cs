using System.Drawing.Drawing2D;

namespace HurricaneBeastPanel;

static partial class WidgetRenderer
{
    /// <summary>A smooth made-up curve (0..1) used while designing and for samples that are not recorded yet.</summary>
    static float Demo(float t, float phase) =>
        Math.Clamp(0.5f + 0.27f * MathF.Sin(MathF.Tau * 1.2f * t + 0.3f + phase) + 0.14f * MathF.Sin(MathF.Tau * 3.3f * t + 1.1f + phase), 0.04f, 0.96f);

    /// <summary>Recent values of <paramref name="bind"/> as 0..1 (NaN where nothing was recorded and no demo is wanted).</summary>
    static float[] Series(Ctx c, string bind, int n, float demoPhase = 0)
    {
        var result = new float[n];
        bool unbound = string.IsNullOrEmpty(bind);
        var history = unbound ? null : SensorHistory.Get(bind, n);
        float span = c.E.BindMax - c.E.BindMin;
        for (int i = 0; i < n; i++)
        {
            if (history != null && !float.IsNaN(history[i]) && span > 0)
                result[i] = Math.Clamp((history[i] - c.E.BindMin) / span, 0f, 1f);
            else if (unbound || c.Preview)
                result[i] = Demo(i / (float)(n - 1), demoPhase);
            else
                result[i] = float.NaN;
        }
        return result;
    }

    static PointF[] Points(float[] s, RectangleF p)
    {
        var pts = new List<PointF>(s.Length);
        float step = p.Width / Math.Max(1, s.Length - 1);
        for (int i = 0; i < s.Length; i++)
            if (!float.IsNaN(s[i])) pts.Add(new PointF(p.Left + i * step, p.Bottom - s[i] * p.Height));
        return pts.ToArray();
    }

    static PointF[] Stairs(PointF[] pts)
    {
        var list = new List<PointF>();
        for (int i = 0; i < pts.Length; i++)
        {
            if (i > 0) list.Add(new PointF(pts[i].X, pts[i - 1].Y));
            list.Add(pts[i]);
        }
        return list.ToArray();
    }

    static void Stroke(Graphics g, PointF[] pts, Brush brush, float width, bool spline)
    {
        if (pts.Length < 2) return;
        using var pen = new Pen(brush, width) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (spline && pts.Length > 2) g.DrawCurve(pen, pts, 0.5f);
        else g.DrawLines(pen, pts);
    }

    static void Stroke(Graphics g, PointF[] pts, Color color, float width, bool spline)
    {
        using var b = new SolidBrush(color);
        Stroke(g, pts, b, width, spline);
    }

    static GraphicsPath AreaPath(PointF[] pts, RectangleF p, bool spline)
    {
        var path = new GraphicsPath();
        if (pts.Length < 2) return path;
        if (spline && pts.Length > 2) path.AddCurve(pts, 0.5f); else path.AddLines(pts);
        path.AddLine(pts[^1].X, pts[^1].Y, pts[^1].X, p.Bottom);
        path.AddLine(pts[^1].X, p.Bottom, pts[0].X, p.Bottom);
        path.CloseFigure();
        return path;
    }

    /// <summary>Area fill that fades from <paramref name="topAlpha"/> at the top to nothing at the bottom.</summary>
    static void FadeFill(Graphics g, GraphicsPath path, RectangleF p, Color color, int topAlpha)
    {
        if (path.PointCount < 3) return;
        using var brush = new LinearGradientBrush(new RectangleF(p.X, p.Y - 1, p.Width, p.Height + 2), A(color, topAlpha), A(color, 0), 90f);
        g.FillPath(brush, path);
    }

    static void DrawGraph(Ctx c)
    {
        var e = c.E;
        var p = RectangleF.Inflate(c.R, -3, -3);
        if (p.Width < 8 || p.Height < 8) return;
        int n = Math.Clamp(e.Samples, 4, 600);
        float lw = Math.Max(1f, e.LineWidth);

        if (e.Style == WidgetStyle.PixelMatrix)
        {
            PixelMatrix(c, p);
        }
        else
        {
            var state = c.G.Save();
            c.G.SetClip(RectangleF.Inflate(p, 5, 5), CombineMode.Intersect);
            GraphBody(c, p, n, lw);
            c.G.Restore(state);
        }

        if (e.BorderEnabled)
        {
            using var frame = RoundRectPath(c.R, e.CornerRadius);
            Border(c, frame);
        }
    }

    static void GraphBody(Ctx c, RectangleF p, int n, float lw)
    {
        var e = c.E;
        var g = c.G;
        var series = Series(c, e.Bind, n);
        var pts = Points(series, p);
        if (pts.Length < 2) return;
        using var mb = c.MainBrush(p);

        switch (e.Style)
        {
            case WidgetStyle.SimpleLine:
                Line(g, new PointF(p.Left, p.Bottom), new PointF(p.Right, p.Bottom), A(c.Main, 40), 1f);
                Stroke(g, pts, mb, lw, false);
                break;

            case WidgetStyle.FilledArea:
            {
                using var area = AreaPath(pts, p, false);
                using var fill = new SolidBrush(A(c.Main, 80));
                g.FillPath(fill, area);
                Stroke(g, pts, mb, lw, false);
                break;
            }

            case WidgetStyle.SteppedLine:
                Stroke(g, Stairs(pts), mb, lw, false);
                break;

            case WidgetStyle.SmoothSpline:
                Stroke(g, pts, mb, lw, true);
                break;

            case WidgetStyle.DualLine:
            {
                PointF[] second;
                if (!string.IsNullOrEmpty(e.Bind2)) second = Points(Series(c, e.Bind2, n, 0.8f), p);
                else
                {
                    // Shadow: the same curve a few samples behind.
                    var shifted = new float[series.Length];
                    for (int i = 0; i < shifted.Length; i++) shifted[i] = series[Math.Max(0, i - 3)];
                    second = Points(shifted, p);
                }
                Stroke(g, second, A(c.Accent, 170), lw, true);
                Stroke(g, pts, mb, lw, true);
                break;
            }

            case WidgetStyle.DottedPoints:
                Stroke(g, pts, A(c.Main, 35), 1f, false);
                foreach (var pt in pts) Dot(g, pt, Math.Max(1.5f, lw * 0.7f), c.Main);
                break;

            case WidgetStyle.GradientFill:
            {
                using var area = AreaPath(pts, p, true);
                FadeFill(g, area, p, c.Main, 210);
                Stroke(g, pts, mb, lw, true);
                break;
            }

            case WidgetStyle.BarSparkline:
            case WidgetStyle.PillBars:
                Bars(c, p, series, pill: e.Style == WidgetStyle.PillBars, mb);
                break;

            case WidgetStyle.GlowingLine:
                foreach (var (extra, alpha) in new[] { (12f, 14), (8f, 24), (4.5f, 44) })
                    Stroke(g, pts, A(c.Main, alpha), lw + extra, true);
                Stroke(g, pts, mb, lw, true);
                Stroke(g, pts, Color.FromArgb(150, 255, 255, 255), Math.Max(1f, lw * 0.35f), true);
                break;

            case WidgetStyle.Oscilloscope:
            {
                using (var panel = RoundRectPath(p, 4))
                using (var tb = c.TrackBrush(p))
                    g.FillPath(tb, panel);
                using (var grid = new Pen(A(c.Main, 40), 1f))
                {
                    for (int i = 1; i < 10; i++) g.DrawLine(grid, p.Left + p.Width * i / 10, p.Top, p.Left + p.Width * i / 10, p.Bottom);
                    for (int i = 1; i < 5; i++) g.DrawLine(grid, p.Left, p.Top + p.Height * i / 5, p.Right, p.Top + p.Height * i / 5);
                }
                Stroke(g, pts, A(c.Main, 40), lw + 4, true);
                Stroke(g, pts, mb, lw, true);
                break;
            }

            case WidgetStyle.SparklineArea:
            {
                using (var area = AreaPath(pts, p, true))
                using (var fill = new SolidBrush(A(c.Main, 34)))
                    g.FillPath(fill, area);
                Stroke(g, pts, mb, Math.Max(1f, lw * 0.8f), true);
                var last = pts[^1];
                Dot(g, last, lw * 2.4f, A(c.Main, 50));
                Dot(g, last, lw * 1.1f, Color.FromArgb(240, 255, 255, 255));
                break;
            }

            case WidgetStyle.SoftFill:
            {
                using var area = AreaPath(pts, p, true);
                FadeFill(g, area, p, c.Main, 150);
                Stroke(g, pts, mb, Math.Max(1f, lw * 0.7f), true);
                break;
            }
        }
    }

    static void Bars(Ctx c, RectangleF p, float[] s, bool pill, Brush gradientBrush)
    {
        int n = s.Length;
        float slot = p.Width / n, w = Math.Max(1.5f, slot * (pill ? 0.62f : 0.78f));
        bool solid = c.E.Fill.Type == FillType.Solid;
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(s[i])) continue;
            float h = Math.Max(2f, s[i] * p.Height);
            var r = new RectangleF(p.Left + i * slot + (slot - w) / 2, p.Bottom - h, w, h);
            // Older samples fade out when the fill is a plain colour.
            using var fade = solid ? new SolidBrush(A(c.Main, 55 + (int)(200 * i / (float)Math.Max(1, n - 1)))) : null;
            Brush brush = fade ?? gradientBrush;
            if (pill)
            {
                using var path = RoundRectPath(r, w / 2);
                c.G.FillPath(brush, path);
            }
            else
            {
                c.G.FillRectangle(brush, r);
            }
        }
    }

    static void PixelMatrix(Ctx c, RectangleF p)
    {
        int rows = Math.Clamp(c.E.Segments, 3, 60);
        int cols = Math.Max(3, (int)Math.Round(rows * p.Width / p.Height));
        float gap = 2f;
        float cell = Math.Min((p.Width - (cols - 1) * gap) / cols, (p.Height - (rows - 1) * gap) / rows);
        if (cell < 1) return;
        float totalW = cols * cell + (cols - 1) * gap, totalH = rows * cell + (rows - 1) * gap;
        float x0 = p.X + (p.Width - totalW) / 2, y0 = p.Y + (p.Height - totalH) / 2;
        float lit = c.F * rows;
        for (int row = 0; row < rows; row++)
        {
            // Row 0 is the bottom row.
            float amount = Math.Clamp(lit - row, 0f, 1f);
            int topRowsDim = rows - row <= 3 ? 1 : 0;
            Color color = amount > 0 ? A(c.Main, 80 + (int)(175 * amount)) : A(c.Main, topRowsDim == 1 ? 38 : 26);
            using var b = new SolidBrush(color);
            for (int col = 0; col < cols; col++)
                c.G.FillRectangle(b, x0 + col * (cell + gap), y0 + (rows - 1 - row) * (cell + gap), cell, cell);
        }
    }
}
