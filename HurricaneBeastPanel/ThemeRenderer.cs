using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace HurricaneBeastPanel;

public static class ThemeRenderer
{
    /// <summary>Renders the theme at its design size scaled to width x height.</summary>
    public static Bitmap Render(Theme theme, SensorSnapshot snap, AppSettings settings, int width, int height, bool preview = false)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.ScaleTransform(width / (float)theme.Width, height / (float)theme.Height);

        var full = new RectangleF(0, 0, theme.Width, theme.Height);
        using (var bg = theme.Background.CreateBrush(full))
            g.FillRectangle(bg, full);

        if (!string.IsNullOrWhiteSpace(theme.BackgroundMedia))
            MediaCache.Get(theme.BackgroundMedia)?.WithFrame(f => DrawMedia(g, f, full, theme.BackgroundMediaFit));

        foreach (var e in theme.Elements)
        {
            if (!e.Visible) continue;
            var state = g.Save();
            try
            {
                if (Math.Abs(e.Rotation) > 0.01f)
                {
                    var c = e.Center;
                    g.TranslateTransform(c.X, c.Y);
                    g.RotateTransform(e.Rotation);
                    g.TranslateTransform(-c.X, -c.Y);
                }
                DrawElement(g, e, snap, settings, preview);
            }
            catch (Exception ex) { Log.Write($"Element '{e.Name}' failed: {ex.Message}"); }
            finally { g.Restore(state); }
        }
        return bmp;
    }

    /// <summary>True if the theme contains a GIF or video, so it needs to be redrawn many times a second.</summary>
    public static bool HasAnimation(Theme theme) =>
        theme.MediaPaths().Any(p => MediaCache.Get(p)?.Animated == true);

    static void DrawElement(Graphics g, Element e, SensorSnapshot snap, AppSettings settings, bool preview)
    {
        var rect = e.Bounds;
        if (rect.Width < 0.5f || rect.Height < 0.5f) return;

        switch (e.Type)
        {
            case ElementType.Text: DrawText(g, e, snap, settings, rect); return;
            case ElementType.Line: DrawLine(g, e, rect); return;
            case ElementType.ChartLinear: DrawLinearChart(g, e, snap, rect); return;
            case ElementType.ChartCircular: DrawRingChart(g, e, snap, rect); return;
            case ElementType.ChartPie: DrawPieChart(g, e, snap, rect); return;
            case ElementType.Media: DrawMediaElement(g, e, rect); return;
            case ElementType.Widget: WidgetRenderer.Draw(g, e, rect, snap, preview); return;
        }

        // Plain shapes. Optional binding scales the width (a simple bar).
        if (!string.IsNullOrEmpty(e.Bind))
        {
            rect.Width *= Fraction(e, snap);
            if (rect.Width < 1f) return;
        }
        FillAndStroke(g, e, rect, e.Type, e.Fill, e.FillEnabled);
    }

    // ---------- values ----------

    /// <summary>Bound value as 0..1. Charts without a binding show 50% so they are visible while designing.</summary>
    static float Fraction(Element e, SensorSnapshot snap)
    {
        if (string.IsNullOrEmpty(e.Bind)) return 0.5f;
        float? v = Tokens.Number(e.Bind, snap);
        float span = e.BindMax - e.BindMin;
        return v.HasValue && span > 0 ? Math.Clamp((v.Value - e.BindMin) / span, 0f, 1f) : 0f;
    }

    // ---------- shapes ----------

    /// <summary>
    /// Soft glow along a path: several wide translucent strokes, widest first, so the light fades out smoothly.
    /// </summary>
    internal static void GlowStroke(Graphics g, GraphicsPath path, string hexColor, float size)
    {
        if (size < 0.5f || path.PointCount < 2) return;
        var color = ColorUtil.Parse(hexColor);
        const int steps = 7;
        for (int i = steps; i >= 1; i--)
        {
            float width = size * 2f * i / steps;
            int alpha = (int)(color.A * (0.025f + 0.045f * (steps - i)));
            using var pen = new Pen(Color.FromArgb(Math.Clamp(alpha, 0, 255), color.R, color.G, color.B), Math.Max(1f, width))
            {
                LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round,
            };
            g.DrawPath(pen, path);
        }
    }

    static void FillAndStroke(Graphics g, Element e, RectangleF rect, ElementType shape, FillSpec fill, bool fillOn)
    {
        // The border is drawn inside the element's bounds.
        float inset = e.BorderEnabled ? e.BorderWidth / 2 : 0;
        var box = RectangleF.Inflate(rect, -inset, -inset);
        if (box.Width < 1 || box.Height < 1) return;

        using var path = ShapePath(shape, box, e.CornerRadius);
        if (e.GlowSize > 0.5f) GlowStroke(g, path, e.GlowColor, e.GlowSize);
        if (fillOn)
        {
            using var brush = fill.CreateBrush(box, inscribed: shape == ElementType.Ellipse);
            g.FillPath(brush, path);
        }
        Stroke(g, e, path);
    }

    static void Stroke(Graphics g, Element e, GraphicsPath path)
    {
        if (!e.BorderEnabled || e.BorderWidth <= 0) return;
        using var pen = new Pen(ColorUtil.Parse(e.BorderColor), e.BorderWidth)
        {
            LineJoin = e.CornerRadius > 0 || e.IsChart ? LineJoin.Round : LineJoin.Miter,
        };
        g.DrawPath(pen, path);
    }

    static void DrawLine(Graphics g, Element e, RectangleF rect)
    {
        if (e.BorderWidth < 0.5f) return;
        using var pen = new Pen(ColorUtil.Parse(e.BorderColor), e.BorderWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        float cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        if (e.GlowSize > 0.5f)
        {
            using var gp = new GraphicsPath();
            if (rect.Width >= rect.Height) gp.AddLine(rect.Left, cy, rect.Right, cy); else gp.AddLine(cx, rect.Top, cx, rect.Bottom);
            GlowStroke(g, gp, e.GlowColor, e.GlowSize);
        }
        if (rect.Width >= rect.Height) g.DrawLine(pen, rect.Left, cy, rect.Right, cy);
        else g.DrawLine(pen, cx, rect.Top, cx, rect.Bottom);
    }

    static void DrawText(Graphics g, Element e, SensorSnapshot snap, AppSettings settings, RectangleF rect)
    {
        string text = Tokens.Expand(e.Text, snap, settings);
        var style = (e.Bold ? FontStyle.Bold : FontStyle.Regular) | (e.Italic ? FontStyle.Italic : FontStyle.Regular);
        using var font = FontStore.Create(e.FontFamily, e.FontSize, style);
        using var brush = e.Fill.CreateBrush(rect);
        using var fmt = new StringFormat
        {
            Alignment = e.Align switch { HAlign.Center => StringAlignment.Center, HAlign.Right => StringAlignment.Far, _ => StringAlignment.Near },
            LineAlignment = StringAlignment.Center,
        };
        if (e.GlowSize > 0.5f)
        {
            using var gp = new GraphicsPath();
            gp.AddString(text, font.FontFamily, (int)font.Style, font.Size, rect, fmt);
            GlowStroke(g, gp, e.GlowColor, e.GlowSize);
        }
        g.DrawString(text, font, brush, rect, fmt);
    }

    // ---------- charts ----------

    /// <summary>Bar: the filled part grows along the long side (left to right, or bottom to top when taller than wide).</summary>
    static void DrawLinearChart(Graphics g, Element e, SensorSnapshot snap, RectangleF rect)
    {
        float inset = e.BorderEnabled ? e.BorderWidth / 2 : 0;
        var box = RectangleF.Inflate(rect, -inset, -inset);
        if (box.Width < 1 || box.Height < 1) return;
        float frac = Fraction(e, snap);

        using var path = ShapePath(ElementType.Rectangle, box, e.CornerRadius);
        if (e.RemainEnabled)
        {
            using var remain = e.RemainFill.CreateBrush(box);
            g.FillPath(remain, path);
        }
        if (e.FillEnabled && frac > 0)
        {
            var value = box.Width >= box.Height
                ? new RectangleF(box.X, box.Y, box.Width * frac, box.Height)
                : new RectangleF(box.X, box.Bottom - box.Height * frac, box.Width, box.Height * frac);
            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect); // keeps the rounded ends when the value is small
            using var brush = e.Fill.CreateBrush(box); // gradient spans the whole bar, the value reveals it
            g.FillRectangle(brush, value);
            g.Restore(state);
        }
        Stroke(g, e, path);
    }

    static void DrawRingChart(Graphics g, Element e, SensorSnapshot snap, RectangleF rect)
    {
        var outer = ChartSquare(e, rect);
        if (outer.Width < 2) return;
        float thickness = Math.Clamp(e.RingThickness, 1f, outer.Width / 2);
        var inner = RectangleF.Inflate(outer, -thickness, -thickness);
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        float valueSweep = sweep * Fraction(e, snap);

        if (e.RemainEnabled && valueSweep < sweep - 0.05f)
        {
            using var path = RingSector(outer, inner, e.StartAngle + valueSweep, sweep - valueSweep);
            using var brush = e.RemainFill.CreateBrush(outer, inscribed: true);
            g.FillPath(brush, path);
        }
        if (e.FillEnabled && valueSweep > 0.05f)
        {
            using var path = RingSector(outer, inner, e.StartAngle, valueSweep);
            using var brush = e.Fill.CreateBrush(outer, inscribed: true);
            g.FillPath(brush, path);
        }
        using var full = RingSector(outer, inner, e.StartAngle, sweep);
        Stroke(g, e, full);
    }

    static void DrawPieChart(Graphics g, Element e, SensorSnapshot snap, RectangleF rect)
    {
        var box = ChartSquare(e, rect);
        if (box.Width < 2) return;
        float sweep = Math.Clamp(e.SweepAngle, 1f, 360f);
        float valueSweep = sweep * Fraction(e, snap);

        if (e.RemainEnabled && valueSweep < sweep - 0.05f)
        {
            using var path = PieSector(box, e.StartAngle + valueSweep, sweep - valueSweep);
            using var brush = e.RemainFill.CreateBrush(box, inscribed: true);
            g.FillPath(brush, path);
        }
        if (e.FillEnabled && valueSweep > 0.05f)
        {
            using var path = PieSector(box, e.StartAngle, valueSweep);
            using var brush = e.Fill.CreateBrush(box, inscribed: true);
            g.FillPath(brush, path);
        }
        using var full = PieSector(box, e.StartAngle, sweep);
        Stroke(g, e, full);
    }

    /// <summary>Largest centred square inside the element, inset for the border.</summary>
    static RectangleF ChartSquare(Element e, RectangleF rect)
    {
        float size = Math.Min(rect.Width, rect.Height);
        float inset = e.BorderEnabled ? e.BorderWidth / 2 : 0;
        var sq = new RectangleF(rect.X + (rect.Width - size) / 2, rect.Y + (rect.Height - size) / 2, size, size);
        return RectangleF.Inflate(sq, -inset, -inset);
    }

    internal static GraphicsPath RingSector(RectangleF outer, RectangleF inner, float start, float sweep)
    {
        var path = new GraphicsPath(FillMode.Alternate);
        bool hasHole = inner.Width >= 1 && inner.Height >= 1;
        if (sweep >= 359.9f)
        {
            path.AddEllipse(outer);
            if (hasHole) path.AddEllipse(inner);
            return path;
        }
        path.AddArc(outer, start, sweep);
        if (hasHole) path.AddArc(inner, start + sweep, -sweep);
        else path.AddLine(path.GetLastPoint(), new PointF(outer.X + outer.Width / 2, outer.Y + outer.Height / 2));
        path.CloseFigure();
        return path;
    }

    internal static GraphicsPath PieSector(RectangleF box, float start, float sweep)
    {
        var path = new GraphicsPath();
        if (sweep >= 359.9f) path.AddEllipse(box);
        else path.AddPie(box.X, box.Y, box.Width, box.Height, start, sweep);
        return path;
    }

    // ---------- media ----------

    static void DrawMediaElement(Graphics g, Element e, RectangleF rect)
    {
        float inset = e.BorderEnabled ? e.BorderWidth / 2 : 0;
        var box = RectangleF.Inflate(rect, -inset, -inset);
        if (box.Width < 1 || box.Height < 1) return;

        using var path = ShapePath(e.FrameShape, box, e.CornerRadius);
        if (e.FillEnabled)
        {
            using var back = e.Fill.CreateBrush(box, inscribed: e.FrameShape == ElementType.Ellipse);
            g.FillPath(back, path);
        }

        bool drawn = MediaCache.Get(e.MediaPath)?.WithFrame(frame =>
        {
            // Draw into a temporary bitmap and fill the frame shape with it: the shape's edge stays antialiased.
            int tw = Math.Max(1, (int)Math.Ceiling(box.Width)), th = Math.Max(1, (int)Math.Ceiling(box.Height));
            using var tmp = new Bitmap(tw, th, PixelFormat.Format32bppPArgb);
            using (var tg = Graphics.FromImage(tmp))
            {
                tg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                tg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawMedia(tg, frame, new RectangleF(0, 0, tw, th), e.Fit);
            }
            using var tex = new TextureBrush(tmp);
            tex.TranslateTransform(box.X, box.Y);
            g.FillPath(tex, path);
        }) == true;

        if (!drawn && !e.FillEnabled)
        {
            using var placeholder = new SolidBrush(Color.FromArgb(255, 44, 48, 62));
            g.FillPath(placeholder, path);
            using var font = new Font("Segoe UI", Math.Max(12, Math.Min(box.Width, box.Height) / 8), GraphicsUnit.Pixel);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            string label = string.IsNullOrWhiteSpace(e.MediaPath) ? "No media selected" : "Media not available";
            g.DrawString(label, font, Brushes.Gainsboro, box, fmt);
        }
        Stroke(g, e, path);
    }

    /// <summary>Draws a frame into <paramref name="dest"/> using the fit mode.</summary>
    static void DrawMedia(Graphics g, Bitmap frame, RectangleF dest, MediaFit fit)
    {
        float sw = frame.Width, sh = frame.Height;
        if (sw < 1 || sh < 1 || dest.Width < 1 || dest.Height < 1) return;

        RectangleF src = new(0, 0, sw, sh);
        RectangleF target = dest;
        switch (fit)
        {
            case MediaFit.Cover:
            {
                float scale = Math.Max(dest.Width / sw, dest.Height / sh);
                float cw = dest.Width / scale, ch = dest.Height / scale;
                src = new RectangleF((sw - cw) / 2, (sh - ch) / 2, cw, ch);
                break;
            }
            case MediaFit.Contain:
            {
                float scale = Math.Min(dest.Width / sw, dest.Height / sh);
                float dw = sw * scale, dh = sh * scale;
                target = new RectangleF(dest.X + (dest.Width - dw) / 2, dest.Y + (dest.Height - dh) / 2, dw, dh);
                break;
            }
        }

        using var attrs = new ImageAttributes();
        attrs.SetWrapMode(WrapMode.TileFlipXY); // avoids a faint seam at the image edge
        var points = new[]
        {
            new PointF(target.Left, target.Top),
            new PointF(target.Right, target.Top),
            new PointF(target.Left, target.Bottom),
        };
        g.DrawImage(frame, points, src, GraphicsUnit.Pixel, attrs);
    }

    // ---------- geometry ----------

    public static GraphicsPath ShapePath(ElementType type, RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        if (type == ElementType.Ellipse)
        {
            path.AddEllipse(r);
            return path;
        }
        AddRoundedPolygon(path, Points(type, r), radius);
        return path;
    }

    static PointF[] Points(ElementType type, RectangleF r)
    {
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        switch (type)
        {
            case ElementType.Triangle:
                return new[] { new PointF(cx, r.Top), new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom) };
            case ElementType.Diamond:
                return new[] { new PointF(cx, r.Top), new PointF(r.Right, cy), new PointF(cx, r.Bottom), new PointF(r.Left, cy) };
            case ElementType.Pentagon:
                return Regular(5, -90, cx, cy, r.Width / 2, r.Height / 2);
            case ElementType.Hexagon:
                return Regular(6, 0, cx, cy, r.Width / 2, r.Height / 2);
            default:
                return new[] { new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom) };
        }
    }

    static PointF[] Regular(int sides, double startDeg, float cx, float cy, float rx, float ry)
    {
        var pts = new PointF[sides];
        for (int i = 0; i < sides; i++)
        {
            double a = (startDeg + i * 360.0 / sides) * Math.PI / 180;
            pts[i] = new PointF(cx + rx * (float)Math.Cos(a), cy + ry * (float)Math.Sin(a));
        }
        return pts;
    }

    /// <summary>Adds a closed polygon with every corner rounded by <paramref name="radius"/> (clamped to fit).</summary>
    static void AddRoundedPolygon(GraphicsPath path, PointF[] pts, float radius)
    {
        if (radius < 0.5f)
        {
            path.AddPolygon(pts);
            return;
        }

        int n = pts.Length;
        var starts = new PointF[n];
        var ends = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            PointF prev = pts[(i + n - 1) % n], cur = pts[i], next = pts[(i + 1) % n];
            float l1 = Dist(cur, prev), l2 = Dist(cur, next);
            if (l1 < 0.01f || l2 < 0.01f)
            {
                starts[i] = ends[i] = cur;
                continue;
            }
            PointF v1 = new((prev.X - cur.X) / l1, (prev.Y - cur.Y) / l1);
            PointF v2 = new((next.X - cur.X) / l2, (next.Y - cur.Y) / l2);
            double cos = Math.Clamp(v1.X * v2.X + v1.Y * v2.Y, -1.0, 1.0);
            double half = Math.Acos(cos) / 2;
            float d = half < 0.01 ? 0 : (float)(radius / Math.Tan(half));
            d = Math.Min(d, Math.Min(l1, l2) / 2);
            starts[i] = new PointF(cur.X + v1.X * d, cur.Y + v1.Y * d);
            ends[i] = new PointF(cur.X + v2.X * d, cur.Y + v2.Y * d);
        }

        path.StartFigure();
        for (int i = 0; i < n; i++)
        {
            PointF cur = pts[i];
            // Quadratic corner curve through the vertex, expressed as a cubic Bezier.
            PointF c1 = new(starts[i].X + (cur.X - starts[i].X) * 2f / 3f, starts[i].Y + (cur.Y - starts[i].Y) * 2f / 3f);
            PointF c2 = new(ends[i].X + (cur.X - ends[i].X) * 2f / 3f, ends[i].Y + (cur.Y - ends[i].Y) * 2f / 3f);
            path.AddBezier(starts[i], c1, c2, ends[i]);
            path.AddLine(ends[i], starts[(i + 1) % n]);
        }
        path.CloseFigure();
    }

    static float Dist(PointF a, PointF b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
