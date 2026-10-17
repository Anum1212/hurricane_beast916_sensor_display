using System.Drawing.Drawing2D;

namespace HurricaneBeastPanel;

/// <summary>
/// Draws the weather picture: sun, moon, clouds, rain, snow, storm or fog, chosen from the current weather (or forced
/// through the element's Bind text for designing). Rain, snow, rays and lightning move a little every frame.
/// </summary>
static class WeatherRenderer
{
    /// <summary>Names the inspector offers for forcing a picture; "" means follow the real weather.</summary>
    public static readonly (string key, string label)[] Choices =
    {
        ("", "Automatic (current weather)"), ("sunny", "Sunny"), ("night", "Clear night"), ("partly", "Partly cloudy"),
        ("cloudy", "Cloudy"), ("fog", "Fog"), ("rain", "Rain"), ("snow", "Snow"), ("storm", "Storm"),
    };

    public static (WeatherKind kind, bool day, bool known) Resolve(Element e, SensorSnapshot snap) => e.Bind switch
    {
        "sunny" => (WeatherKind.Clear, true, true),
        "night" => (WeatherKind.Clear, false, true),
        "partly" => (WeatherKind.PartlyCloudy, true, true),
        "cloudy" => (WeatherKind.Cloudy, true, true),
        "fog" => (WeatherKind.Fog, true, true),
        "rain" => (WeatherKind.Rain, true, true),
        "snow" => (WeatherKind.Snow, true, true),
        "storm" => (WeatherKind.Storm, true, true),
        _ => snap.Weather is { } w ? (w.Kind, w.IsDay, true) : (WeatherKind.Cloudy, true, false),
    };

    public static void Draw(Graphics g, Element e, RectangleF rect, SensorSnapshot snap, AppSettings settings)
    {
        WeatherService.Touch(settings.WeatherLocation);
        var (kind, day, known) = Resolve(e, snap);
        float t = (Environment.TickCount64 % 3_600_000) / 1000f;

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float size = Math.Min(rect.Width, rect.Height);
        g.TranslateTransform(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        g.ScaleTransform(size / 100f, size / 100f);

        if (!known) DrawCloud(g, 0, 0, 1.15f, Pale, false, 90);
        else switch (kind)
        {
            case WeatherKind.Clear:
                if (day) DrawSun(g, 0, 0, 24, t); else DrawMoon(g, 0, 0, 28, t, true);
                break;
            case WeatherKind.PartlyCloudy:
                if (day) DrawSun(g, 14, -14, 17, t); else DrawMoon(g, 14, -14, 19, t, false);
                DrawCloud(g, -5, 8, 0.95f, Pale, false);
                break;
            case WeatherKind.Cloudy:
                DrawCloud(g, 14, -14, 0.7f, Grey, false);
                DrawCloud(g, -4, 8, 1.0f, Pale, false);
                break;
            case WeatherKind.Fog:
                DrawCloud(g, 0, -14, 0.95f, Pale, false);
                DrawFog(g, t);
                break;
            case WeatherKind.Rain:
                DrawCloud(g, 0, -14, 1.05f, Dark, false);
                DrawRain(g, t, 7, 0f);
                break;
            case WeatherKind.Snow:
                DrawCloud(g, 0, -14, 1.05f, Grey, false);
                DrawSnow(g, t);
                break;
            case WeatherKind.Storm:
                DrawCloud(g, 0, -16, 1.05f, Storm, false);
                DrawRain(g, t, 4, 8f);
                DrawBolt(g, t);
                break;
        }
        g.Restore(state);
    }

    // palettes: top colour, bottom colour
    static readonly (Color a, Color b) Pale = (Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 196, 208, 228));
    static readonly (Color a, Color b) Grey = (Color.FromArgb(255, 214, 222, 236), Color.FromArgb(255, 150, 163, 186));
    static readonly (Color a, Color b) Dark = (Color.FromArgb(255, 188, 199, 218), Color.FromArgb(255, 118, 132, 158));
    static readonly (Color a, Color b) Storm = (Color.FromArgb(255, 150, 160, 184), Color.FromArgb(255, 78, 88, 112));

    static void Disc(Graphics g, Brush b, float cx, float cy, float r) => g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);

    static void DrawCloud(Graphics g, float cx, float cy, float k, (Color a, Color b) palette, bool outline, int alpha = 255)
    {
        var box = new RectangleF(cx - 33 * k, cy - 26 * k, 66 * k, 44 * k);
        using var brush = new LinearGradientBrush(box, Color.FromArgb(alpha, palette.a), Color.FromArgb(alpha, palette.b), 90f);
        Disc(g, brush, cx - 19 * k, cy + 3 * k, 13 * k);
        Disc(g, brush, cx - 2 * k, cy - 6 * k, 19 * k);
        Disc(g, brush, cx + 19 * k, cy + 2 * k, 14 * k);
        g.FillRectangle(brush, cx - 19 * k, cy - 4 * k, 38 * k, 20 * k);
    }

    static void DrawSun(Graphics g, float cx, float cy, float r, float t)
    {
        using (var glow = new SolidBrush(Color.FromArgb(34, 255, 200, 70)))
        {
            Disc(g, glow, cx, cy, r * 1.9f);
            Disc(g, glow, cx, cy, r * 1.5f);
        }
        using var ray = new Pen(Color.FromArgb(255, 255, 196, 60), Math.Max(2f, r * 0.17f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float spin = t * 14f;
        for (int i = 0; i < 8; i++)
        {
            double a = (spin + i * 45f) * Math.PI / 180;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            float r0 = r * (1.32f + 0.04f * MathF.Sin(t * 2 + i)), r1 = r * 1.72f;
            g.DrawLine(ray, cx + cos * r0, cy + sin * r0, cx + cos * r1, cy + sin * r1);
        }
        var box = new RectangleF(cx - r, cy - r, r * 2, r * 2);
        using var body = new LinearGradientBrush(box, Color.FromArgb(255, 255, 232, 130), Color.FromArgb(255, 255, 160, 40), 55f);
        g.FillEllipse(body, box);
    }

    static void DrawMoon(Graphics g, float cx, float cy, float r, float t, bool stars)
    {
        var box = new RectangleF(cx - r, cy - r, r * 2, r * 2);
        using var moon = new GraphicsPath();
        moon.AddEllipse(box);
        using var bite = new GraphicsPath();
        bite.AddEllipse(cx - r * 0.55f + r * 0.62f, cy - r * 1.0f - r * 0.12f, r * 1.7f, r * 1.7f);
        using var region = new Region(moon);
        region.Exclude(bite);
        using (var glow = new SolidBrush(Color.FromArgb(12, 190, 210, 255))) Disc(g, glow, cx, cy, r * 1.5f);
        using (var body = new LinearGradientBrush(box, Color.FromArgb(255, 250, 247, 220), Color.FromArgb(255, 196, 206, 236), 60f))
            g.FillRegion(body, region);
        if (!stars) return;
        for (int i = 0; i < 5; i++)
        {
            float sx = cx + new[] { 26f, 34f, 20f, -34f, -22f }[i], sy = cy + new[] { -22f, 6f, 30f, -26f, 26f }[i];
            float tw = 0.5f + 0.5f * MathF.Sin(t * 2.2f + i * 1.7f), s = 1.4f + tw * 1.6f;
            using var star = new SolidBrush(Color.FromArgb((int)(120 + 135 * tw), 255, 255, 255));
            Disc(g, star, sx, sy, s);
        }
    }

    static void DrawRain(Graphics g, float t, int drops, float shift)
    {
        using var pen = new Pen(Color.FromArgb(255, 80, 170, 255), 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (int i = 0; i < drops; i++)
        {
            float phase = (t * 1.1f + i * 0.618f) % 1f;
            float x = -26 + i * (52f / Math.Max(1, drops - 1)) + phase * 5 + shift * 0.2f, y = 12 + phase * 34;
            int alpha = (int)(255 * (phase < 0.8f ? 1f : (1 - phase) / 0.2f));
            pen.Color = Color.FromArgb(Math.Clamp(alpha, 0, 255), 80, 170, 255);
            g.DrawLine(pen, x, y, x - 3f, y + 8f);
        }
    }

    static void DrawSnow(Graphics g, float t)
    {
        for (int i = 0; i < 8; i++)
        {
            float phase = (t * 0.32f + i * 0.382f) % 1f;
            float x = -27 + i * 7.7f + MathF.Sin(t * 1.8f + i * 2) * 3f, y = 12 + phase * 36;
            int alpha = (int)(255 * (phase < 0.8f ? 1f : (1 - phase) / 0.2f));
            using var b = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 0, 255), 255, 255, 255));
            Disc(g, b, x, y, 2.4f + (i % 3) * 0.5f);
        }
    }

    static void DrawBolt(Graphics g, float t)
    {
        if ((t * 0.8f) % 1f > 0.82f) return; // brief flicker off
        var bolt = new[] { new PointF(4, 10), new PointF(-9, 31), new PointF(-1, 31), new PointF(-7, 49), new PointF(12, 24), new PointF(3, 24), new PointF(9, 10) };
        var box = new RectangleF(-9, 10, 21, 39);
        using var fill = new LinearGradientBrush(box, Color.FromArgb(255, 255, 240, 120), Color.FromArgb(255, 255, 178, 30), 90f);
        g.FillPolygon(fill, bolt);
    }

    static void DrawFog(Graphics g, float t)
    {
        using var pen = new Pen(Color.FromArgb(200, 200, 212, 232), 4.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (int i = 0; i < 3; i++)
        {
            float dx = MathF.Sin(t * 0.7f + i * 1.4f) * 5f, y = 14 + i * 11;
            g.DrawLine(pen, -28 + dx + i * 3, y, 28 + dx - i * 3, y);
        }
    }
}
