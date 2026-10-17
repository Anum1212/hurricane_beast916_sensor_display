using System.Drawing.Drawing2D;

namespace HurricaneBeastPanel;

static partial class WidgetRenderer
{
    static void DrawMeter(Ctx c)
    {
        var g = c.G;
        // Bars are drawn upright (filling bottom to top). A wide element turns the drawing 90 degrees so it fills left to right.
        bool horizontal = c.Info.Rotates && c.R.Width > c.R.Height * 1.15f;
        if (!horizontal)
        {
            MeterBody(c, c.R);
            return;
        }
        var state = g.Save();
        var ctr = Center(c.R);
        g.TranslateTransform(ctr.X, ctr.Y);
        g.RotateTransform(90);
        MeterBody(c, new RectangleF(-c.R.Height / 2, -c.R.Width / 2, c.R.Height, c.R.Width));
        g.Restore(state);
    }

    /// <summary>The part of <paramref name="area"/> below the level (a fraction of its height, filled from the bottom).</summary>
    static RectangleF Level(RectangleF area, float f) =>
        new(area.X, area.Bottom - area.Height * Math.Clamp(f, 0f, 1f), area.Width, area.Height * Math.Clamp(f, 0f, 1f));

    static void FillClipped(Ctx c, GraphicsPath clip, RectangleF fillRect, Brush brush)
    {
        if (fillRect.Height < 0.5f) return;
        var state = c.G.Save();
        c.G.SetClip(clip, CombineMode.Intersect);
        c.G.FillRectangle(brush, fillRect);
        c.G.Restore(state);
    }

    static void TrackFill(Ctx c, GraphicsPath path, RectangleF bounds)
    {
        using var tb = c.TrackBrush(bounds);
        c.G.FillPath(tb, path);
    }

    /// <summary>Thin outline of the meter's container unless the user set their own border.</summary>
    static void Outline(Ctx c, GraphicsPath path, int defaultAlpha = 0)
    {
        if (c.E.BorderEnabled) { Border(c, path); return; }
        if (defaultAlpha <= 0) return;
        using var pen = new Pen(A(c.Main, defaultAlpha), 2f) { LineJoin = LineJoin.Round };
        c.G.DrawPath(pen, path);
    }

    static void MeterBody(Ctx c, RectangleF r)
    {
        var g = c.G;
        var e = c.E;
        float f = c.F;
        switch (e.Style)
        {
            case WidgetStyle.TankLevel: Tank(c, r, wave: false); break;
            case WidgetStyle.WaveFill: Tank(c, r, wave: true); break;
            case WidgetStyle.LedArray: Blocks(c, r, led: true); break;
            case WidgetStyle.LcdBlocks: Blocks(c, r, led: false); break;
            case WidgetStyle.Thermometer: Thermometer(c, r); break;
            case WidgetStyle.Battery: Battery(c, r); break;
            case WidgetStyle.SignalStrength: Signal(c, r); break;
            case WidgetStyle.Cylinder3D: Cylinder(c, r); break;
            case WidgetStyle.DiamondMeter: Diamond(c, r); break;
            case WidgetStyle.StackedBars: StackedBars(c, r); break;

            case WidgetStyle.SolidMeter:
            case WidgetStyle.GradientMeter:
            {
                using var path = new GraphicsPath();
                path.AddRectangle(r);
                TrackFill(c, path, r);
                using var mb = c.MainBrush(r);
                g.FillRectangle(mb, Level(r, f));
                Outline(c, path);
                break;
            }

            case WidgetStyle.FlatFill:
            {
                var bar = new RectangleF(r.X + r.Width * 0.2f, r.Y, r.Width * 0.6f, r.Height);
                using var path = new GraphicsPath();
                path.AddRectangle(bar);
                TrackFill(c, path, bar);
                using var mb = c.MainBrush(bar);
                g.FillRectangle(mb, Level(bar, f));
                Outline(c, path);
                break;
            }

            case WidgetStyle.SegmentedMeter: Lines(c, r, rounded: false); break;
            case WidgetStyle.NotchBar: Lines(c, r, rounded: true); break;

            case WidgetStyle.NeonGlowMeter: NeonKnob(c, r); break;
            case WidgetStyle.ThinRailDot: RailDot(c, r); break;
            case WidgetStyle.SlimBar: SlimBar(c, r); break;
            case WidgetStyle.DualComparison: DualComparison(c, r); break;
            case WidgetStyle.DualSided: DualSided(c, r); break;
            case WidgetStyle.ZoneBar: ZoneBar(c, r); break;
            case WidgetStyle.GradientGlow: GradientGlow(c, r); break;
            case WidgetStyle.StripedFill: Striped(c, r); break;
            case WidgetStyle.Capsule: Capsule(c, r); break;
            case WidgetStyle.RoundedPill: RoundedPill(c, r); break;
            case WidgetStyle.NeonOutline: NeonOutline(c, r); break;
        }
    }

    // ---------- liquid containers ----------

    static void Tank(Ctx c, RectangleF r, bool wave)
    {
        var g = c.G;
        float radius = Math.Max(2, c.E.CornerRadius);
        using var body = RoundRectPath(r, radius);
        TrackFill(c, body, r);

        var level = Level(r, c.F);
        using var mb = c.MainBrush(r);
        if (wave && level.Height > 1)
        {
            float amp = Math.Min(r.Height * 0.035f, 10f), len = Math.Max(r.Width / 1.6f, 20f);
            double phase = Environment.TickCount64 / 700.0 % (Math.PI * 2);
            var pts = new List<PointF>();
            for (float x = r.Left; x <= r.Right + 2; x += 3)
                pts.Add(new PointF(x, level.Top + amp * (float)Math.Sin((x - r.Left) / len * Math.PI * 2 + phase)));
            pts.Add(new PointF(r.Right + 2, r.Bottom + 2));
            pts.Add(new PointF(r.Left - 2, r.Bottom + 2));
            using var liquid = new GraphicsPath();
            liquid.AddPolygon(pts.ToArray());
            var state = g.Save();
            g.SetClip(body, CombineMode.Intersect);
            g.FillPath(mb, liquid);
            g.Restore(state);
        }
        else
        {
            FillClipped(c, body, level, mb);
            if (level.Height > 2)
                using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 2f))
                    g.DrawLine(pen, level.Left + radius * 0.4f, level.Top, level.Right - radius * 0.4f, level.Top);
        }

        if (!wave)
        {
            // Rivets in the corners.
            float m = Math.Min(r.Width, r.Height) * 0.07f;
            foreach (var p in new[] { new PointF(r.Left + m, r.Top + m), new PointF(r.Right - m, r.Top + m), new PointF(r.Left + m, r.Bottom - m), new PointF(r.Right - m, r.Bottom - m) })
                Dot(g, p, 2.2f, A(c.Main, 170));
        }
        Outline(c, body, 170);
    }

    static void Thermometer(Ctx c, RectangleF r)
    {
        var g = c.G;
        float cx = r.X + r.Width / 2;
        float bulbD = Math.Min(r.Width * 0.92f, r.Height * 0.3f);
        var bulb = new RectangleF(cx - bulbD / 2, r.Bottom - bulbD, bulbD, bulbD);
        float tubeW = Math.Max(6f, bulbD * 0.42f);
        var tube = new RectangleF(cx - tubeW / 2, r.Top, tubeW, bulb.Y + bulbD * 0.3f - r.Top);

        using var shape = new GraphicsPath(FillMode.Winding);
        using (var tubePath = RoundRectPath(tube, tubeW / 2)) shape.AddPath(tubePath, false);
        shape.AddEllipse(bulb);
        TrackFill(c, shape, r);

        // The scale runs from the bulb centre to the tube top.
        float bottomY = bulb.Y + bulbD / 2, topY = tube.Top + tubeW / 2;
        float levelY = bottomY - (bottomY - topY) * c.F;
        using var mb = c.MainBrush(r);
        FillClipped(c, shape, new RectangleF(r.X, levelY, r.Width, r.Bottom - levelY), mb);
        // Inner tube highlight.
        using (var hi = new Pen(Color.FromArgb(60, 255, 255, 255), Math.Max(1.5f, tubeW * 0.18f)))
            g.DrawLine(hi, cx - tubeW * 0.18f, Math.Min(levelY, bottomY) + 2, cx - tubeW * 0.18f, bottomY);
        Outline(c, shape, 130);
    }

    static void Cylinder(Ctx c, RectangleF r)
    {
        var g = c.G;
        float eh = Math.Min(r.Width * 0.3f, r.Height * 0.3f);
        var top = new RectangleF(r.X, r.Y, r.Width, eh);
        var bottom = new RectangleF(r.X, r.Bottom - eh, r.Width, eh);
        using var shape = new GraphicsPath(FillMode.Winding);
        shape.AddEllipse(top);
        shape.AddEllipse(bottom);
        shape.AddRectangle(new RectangleF(r.X, top.Y + eh / 2, r.Width, bottom.Y - top.Y));
        TrackFill(c, shape, r);

        float topY = top.Y + eh / 2, botY = bottom.Y + eh / 2;
        float levelY = botY - (botY - topY) * c.F;
        using var mb = c.MainBrush(r);
        FillClipped(c, shape, new RectangleF(r.X, levelY, r.Width, r.Bottom - levelY), mb);

        // Cylindrical shading across the whole body.
        using (var shade = new LinearGradientBrush(r, Color.FromArgb(90, 0, 0, 0), Color.FromArgb(90, 0, 0, 0), 0f))
        {
            shade.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.FromArgb(90, 0, 0, 0), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(90, 0, 0, 0) },
                Positions = new[] { 0f, 0.28f, 0.4f, 0.6f, 1f },
            };
            var state = g.Save();
            g.SetClip(shape, CombineMode.Intersect);
            g.FillRectangle(shade, r);
            g.Restore(state);
        }
        // Liquid surface.
        if (c.F > 0.01f)
        {
            var surface = new RectangleF(r.X, levelY - eh / 2, r.Width, eh);
            using var sb = new SolidBrush(Mix(c.Main, Color.White, 0.35f));
            g.FillEllipse(sb, surface);
        }
        using (var pen = new Pen(A(c.Main, 150), 1.6f))
        {
            g.DrawEllipse(pen, top);
            g.DrawArc(pen, bottom, 0, 180);
            g.DrawLine(pen, r.Left, topY, r.Left, botY);
            g.DrawLine(pen, r.Right, topY, r.Right, botY);
        }
        if (c.E.BorderEnabled) Border(c, shape);
    }

    static void Diamond(Ctx c, RectangleF r)
    {
        var g = c.G;
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        using var path = new GraphicsPath();
        path.AddPolygon(new[] { new PointF(cx, r.Top), new PointF(r.Right, cy), new PointF(cx, r.Bottom), new PointF(r.Left, cy) });
        TrackFill(c, path, r);
        using var mb = c.MainBrush(r);
        var level = Level(r, c.F);
        FillClipped(c, path, level, mb);
        if (level.Height > 1)
        {
            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect); // keep the level line inside the diamond
            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f))
                g.DrawLine(pen, r.Left, level.Top, r.Right, level.Top);
            g.Restore(state);
        }
        Dot(g, new PointF(cx, r.Top), 3f, A(c.Main, 220));
        Outline(c, path, 200);
    }

    // ---------- block and line stacks ----------

    static void Blocks(Ctx c, RectangleF r, bool led)
    {
        int n = Math.Clamp(c.E.Segments, 2, 60);
        float gap = Math.Max(2f, r.Height / n * 0.22f);
        float h = (r.Height - gap * (n - 1)) / n;
        if (h < 1) return;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            bool lit = t <= c.F;
            Color color = !lit ? A(c.Main, 26) : !led ? c.Main : t < 0.7f ? c.Main : t < 0.88f ? Amber : c.Accent;
            using var b = new SolidBrush(color);
            var block = new RectangleF(r.X, r.Bottom - (i + 1) * h - i * gap, r.Width, h);
            if (led)
            {
                using var path = RoundRectPath(block, Math.Min(3f, h / 2));
                c.G.FillPath(b, path);
            }
            else
            {
                c.G.FillRectangle(b, block);
            }
        }
        if (c.E.BorderEnabled)
        {
            using var frame = new GraphicsPath();
            frame.AddRectangle(r);
            Border(c, frame);
        }
    }

    /// <summary>Horizontal bars stacked up the meter: thin lines (segmented) or thick rounded notches.</summary>
    static void Lines(Ctx c, RectangleF r, bool rounded)
    {
        int n = Math.Clamp(c.E.Segments, 2, 80);
        float gap = rounded ? r.Height / n * 0.45f : r.Height / n * 0.6f;
        float h = (r.Height - gap * (n - 1)) / n;
        if (h < 0.8f) return;
        using var mb = c.MainBrush(r);
        for (int i = 0; i < n; i++)
        {
            bool lit = (i + 0.5f) / n <= c.F;
            var line = new RectangleF(r.X, r.Bottom - (i + 1) * h - i * gap, r.Width, h);
            using var dim = new SolidBrush(A(c.Main, 28));
            using var path = rounded ? RoundRectPath(line, h / 2) : null;
            if (path != null) c.G.FillPath(lit ? mb : dim, path);
            else c.G.FillRectangle(lit ? mb : dim, line);
        }
        if (c.E.BorderEnabled)
        {
            using var frame = RoundRectPath(r, rounded ? 6 : 0);
            Border(c, frame);
        }
    }

    static void Battery(Ctx c, RectangleF r)
    {
        var g = c.G;
        float capH = r.Height * 0.06f, capW = r.Width * 0.38f;
        var body = new RectangleF(r.X, r.Y + capH, r.Width, r.Height - capH);
        using (var cap = RoundRectPath(new RectangleF(r.X + (r.Width - capW) / 2, r.Y, capW, capH + 2), 2))
        using (var b = new SolidBrush(A(c.Main, 190)))
            g.FillPath(b, cap);
        using var shell = RoundRectPath(body, Math.Min(r.Width * 0.12f, 14));
        TrackFill(c, shell, body);

        float pad = Math.Max(4f, r.Width * 0.09f);
        var inner = RectangleF.Inflate(body, -pad, -pad);
        int n = Math.Clamp(c.E.Segments, 3, 30);
        float gap = Math.Max(2f, inner.Height / n * 0.18f);
        float h = (inner.Height - gap * (n - 1)) / n;
        Color lit = c.F > 0.3f ? c.Main : c.F > 0.15f ? Amber : c.Accent;
        for (int i = 0; i < n; i++)
        {
            bool on = (i + 0.5f) / n <= c.F;
            using var b = new SolidBrush(on ? lit : A(c.Main, 22));
            g.FillRectangle(b, inner.X, inner.Bottom - (i + 1) * h - i * gap, inner.Width, h);
        }
        if (c.E.BorderEnabled) Border(c, shell);
        else
        {
            using var pen = new Pen(A(c.Main, 200), 2.5f);
            g.DrawPath(pen, shell);
        }
    }

    static void Signal(Ctx c, RectangleF r)
    {
        int n = Math.Clamp(c.E.Segments, 2, 40);
        float gap = r.Width / n * 0.18f;
        float w = (r.Width - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            float h = r.Height * (0.22f + 0.78f * (i + 1) / n);
            var bar = new RectangleF(r.X + i * (w + gap), r.Bottom - h, w, h);
            bool lit = (i + 0.5f) / n <= c.F;
            using var b = new SolidBrush(lit ? c.Main : A(c.Main, 48));
            using var path = RoundRectPath(bar, Math.Min(4f, w / 3));
            c.G.FillPath(b, path);
        }
        if (c.E.BorderEnabled)
        {
            using var frame = RoundRectPath(r, 4);
            Border(c, frame);
        }
    }

    static void StackedBars(Ctx c, RectangleF r)
    {
        // Three horizontal bars (always left to right).
        float gap = r.Height * 0.1f, h = (r.Height - gap * 2) / 3;
        var values = new[] { c.F, c.F2, c.F3 };
        for (int i = 0; i < 3; i++)
        {
            var bar = new RectangleF(r.X, r.Y + i * (h + gap), r.Width, h);
            float radius = Math.Max(c.E.CornerRadius, 0f);
            using var path = RoundRectPath(bar, Math.Min(radius, h / 2));
            TrackFill(c, path, bar);
            var fill = new RectangleF(bar.X, bar.Y, bar.Width * values[i], bar.Height);
            using var mb = c.MainBrush(bar);
            FillClipped(c, path, fill, mb);
            Outline(c, path);
        }
    }

    // ---------- bars with details ----------

    static void NeonKnob(Ctx c, RectangleF r)
    {
        var g = c.G;
        float d = r.Width;
        using var pill = RoundRectPath(r, d / 2);
        TrackFill(c, pill, r);
        float cy = r.Bottom - d / 2 - (r.Height - d) * c.F;
        // Lit part up to the knob.
        using (var lit = new SolidBrush(A(c.Main, 70)))
            FillClipped(c, pill, new RectangleF(r.X, cy, r.Width, r.Bottom - cy), lit);
        var ctr = new PointF(r.X + r.Width / 2, cy);
        for (int i = 4; i >= 1; i--) Dot(g, ctr, d / 2 + i * d * 0.07f, A(c.Main, 14 + (5 - i) * 6));
        using (var kb = c.MainBrush(new RectangleF(ctr.X - d / 2, cy - d / 2, d, d), true))
            g.FillEllipse(kb, ctr.X - d / 2, cy - d / 2, d, d);
        using (var hi = RoundRectPath(new RectangleF(ctr.X - d * 0.12f, cy - d * 0.34f, d * 0.24f, d * 0.68f), d * 0.12f))
        using (var hb = new SolidBrush(Color.FromArgb(95, 255, 255, 255)))
            g.FillPath(hb, hi);
        Outline(c, pill);
    }

    static void RailDot(Ctx c, RectangleF r)
    {
        var g = c.G;
        float cx = r.X + r.Width / 2;
        float dotR = Math.Clamp(r.Width * 0.3f, 7f, 22f);
        float y = r.Bottom - dotR - (r.Height - dotR * 2) * c.F;
        Line(g, new PointF(cx, r.Top), new PointF(cx, r.Bottom), A(c.Main, 55), 2f);
        using (var b = c.MainBrush(r)) using (var pen = new Pen(b, 3.5f)) g.DrawLine(pen, cx, y, cx, r.Bottom);
        var ctr = new PointF(cx, y);
        Dot(g, ctr, dotR * 1.7f, A(c.Main, 45));
        Dot(g, ctr, dotR, c.Main);
        Dot(g, ctr, dotR * 0.45f, Color.FromArgb(235, 255, 255, 255));
        if (c.E.BorderEnabled)
        {
            using var frame = RoundRectPath(r, 4);
            Border(c, frame);
        }
    }

    static void SlimBar(Ctx c, RectangleF r)
    {
        var g = c.G;
        float w = Math.Max(4f, r.Width * 0.14f), cx = r.X + r.Width / 2;
        var bar = new RectangleF(cx - w / 2, r.Y, w, r.Height);
        using var path = RoundRectPath(bar, w / 2);
        TrackFill(c, path, bar);
        using var mb = c.MainBrush(bar);
        var level = Level(bar, c.F);
        FillClipped(c, path, level, mb);
        Dot(g, new PointF(cx, level.Top), w * 0.85f, c.Main);
        Outline(c, path);
    }

    static void DualComparison(Ctx c, RectangleF r)
    {
        var g = c.G;
        float gap = r.Width * 0.12f, w = (r.Width - gap) / 2;
        var values = new[] { c.F, c.F2 };
        for (int i = 0; i < 2; i++)
        {
            var bar = new RectangleF(r.X + i * (w + gap), r.Y, w, r.Height);
            using var path = new GraphicsPath();
            path.AddRectangle(bar);
            TrackFill(c, path, bar);
            var level = Level(bar, values[i]);
            using var brush = i == 0 ? c.MainBrush(bar) : new SolidBrush(c.Accent);
            g.FillRectangle(brush, level);
            if (level.Height > 1)
                using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1.5f))
                    g.DrawLine(pen, bar.Left, level.Top, bar.Right, level.Top);
            Outline(c, path);
        }
    }

    static void DualSided(Ctx c, RectangleF r)
    {
        var g = c.G;
        float mid = r.Y + r.Height / 2, half = r.Height / 2;
        var upper = new RectangleF(r.X, r.Y, r.Width, half);
        var lower = new RectangleF(r.X, mid, r.Width, half);
        using (var path = new GraphicsPath())
        {
            path.AddRectangle(r);
            TrackFill(c, path, r);
            Outline(c, path);
        }
        // Up from the centre line: first value (accent); down: second value (main).
        using (var up = new SolidBrush(c.Accent))
            g.FillRectangle(up, upper.X, mid - half * c.F, upper.Width, half * c.F);
        using (var down = c.MainBrush(lower))
            g.FillRectangle(down, lower.X, mid, lower.Width, half * c.F2);
        using var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawLine(pen, r.Left, mid, r.Right, mid);
    }

    static void ZoneBar(Ctx c, RectangleF r)
    {
        var g = c.G;
        using (var b = new SolidBrush(A(Green, 45))) g.FillRectangle(b, r.X, r.Bottom - r.Height * 0.6f, r.Width, r.Height * 0.6f);
        using (var b = new SolidBrush(A(Amber, 45))) g.FillRectangle(b, r.X, r.Bottom - r.Height * 0.85f, r.Width, r.Height * 0.25f);
        using (var b = new SolidBrush(A(Red, 45))) g.FillRectangle(b, r.X, r.Y, r.Width, r.Height * 0.15f);
        var level = Level(r, c.F);
        using (var b = new SolidBrush(Zone(c.F))) g.FillRectangle(b, level);
        if (level.Height > 1)
            using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f))
                g.DrawLine(pen, r.Left, level.Top, r.Right, level.Top);
        using var path = new GraphicsPath();
        path.AddRectangle(r);
        Outline(c, path);
    }

    static void GradientGlow(Ctx c, RectangleF r)
    {
        var g = c.G;
        using var pill = RoundRectPath(r, r.Width / 2);
        TrackFill(c, pill, r);
        var level = Level(r, c.F);
        if (level.Height > 2)
        {
            using var fillPath = RoundRectPath(level, Math.Min(r.Width / 2, level.Height / 2));
            foreach (var (extra, alpha) in new[] { (12f, 12), (8f, 20), (4f, 34) })
                using (var pen = new Pen(A(c.Main, alpha), extra) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, fillPath);
            using var mb = c.MainBrush(r);
            g.FillPath(mb, fillPath);
        }
        Outline(c, pill);
    }

    static void Striped(Ctx c, RectangleF r)
    {
        var g = c.G;
        using var path = new GraphicsPath();
        path.AddRectangle(r);
        TrackFill(c, path, r);
        var level = Level(r, c.F);
        if (level.Height > 1)
        {
            using var mb = c.MainBrush(r);
            g.FillRectangle(mb, level);
            var state = g.Save();
            g.SetClip(level, CombineMode.Intersect);
            using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 6f))
                for (float x = r.Left - r.Height; x < r.Right; x += 16)
                    g.DrawLine(pen, x, r.Bottom, x + r.Height, r.Bottom - r.Height);
            g.Restore(state);
        }
        Outline(c, path);
    }

    static void Capsule(Ctx c, RectangleF r)
    {
        var g = c.G;
        using var pill = RoundRectPath(r, r.Width / 2);
        TrackFill(c, pill, r);
        using var mb = c.MainBrush(r);
        FillClipped(c, pill, Level(r, c.F), mb);
        // Glossy highlight down the left side.
        var state = g.Save();
        g.SetClip(pill, CombineMode.Intersect);
        using (var gloss = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
            g.FillRectangle(gloss, r.X + r.Width * 0.12f, r.Y, r.Width * 0.18f, r.Height);
        g.Restore(state);
        Outline(c, pill);
    }

    static void RoundedPill(Ctx c, RectangleF r)
    {
        var g = c.G;
        using var pill = RoundRectPath(r, r.Width / 2);
        TrackFill(c, pill, r);
        float pad = r.Width * 0.14f;
        float innerW = r.Width - pad * 2;
        float maxH = r.Height - pad * 2;
        float h = Math.Max(innerW, maxH * c.F);
        if (c.F < 0.005f) { Outline(c, pill); return; }
        var blob = new RectangleF(r.X + pad, r.Bottom - pad - h, innerW, h);
        using var path = RoundRectPath(blob, innerW / 2);
        using var mb = c.MainBrush(r);
        g.FillPath(mb, path);
        Outline(c, pill);
    }

    static void NeonOutline(Ctx c, RectangleF r)
    {
        var g = c.G;
        using var path = new GraphicsPath();
        path.AddRectangle(r);
        TrackFill(c, path, r);
        var level = Level(r, c.F);
        using (var below = new SolidBrush(A(c.Main, 55))) g.FillRectangle(below, level);
        if (level.Height > 1)
        {
            foreach (var (extra, alpha) in new[] { (10f, 14), (6f, 28) })
                using (var pen = new Pen(A(c.Main, alpha), extra)) g.DrawLine(pen, r.Left, level.Top, r.Right, level.Top);
            using var mb = c.MainBrush(r);
            using var line = new Pen(mb, 5f);
            g.DrawLine(line, r.Left, level.Top, r.Right, level.Top);
        }
        if (c.E.BorderEnabled) Border(c, path);
        else using (var pen = new Pen(A(c.Main, 190), 3f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
    }
}
