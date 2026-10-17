namespace HurricaneBeastPanel;

/// <summary>
/// "Rainbow" dashboard: grey tiles, blue-pink-orange gradient bars and rings, a temperature history frame per chip.
/// Built in dark and light, vertical (462x1920) and horizontal (1920x462), only from editor elements.
/// </summary>
static class RainbowThemes
{
    sealed record Look(string Bg1, string Bg2, string Tile, string Text, string Label, string Track, string Edge, string Blue);

    static readonly Look DarkLook = new("#FF1E1E1E", "#FF080808", "#FF262626", "#FFFFFFFF", "#FFDADADA", "#FF474747", "#FF8C8C8C", "#FF3D8BFF");
    static readonly Look LightLook = new("#FFF2F2F2", "#FFD6D6D6", "#FFE2E2E2", "#FF111111", "#FF2C2C2C", "#FFC6C6C6", "#FF5A5A5A", "#FF1F78F5");

    const string Strong = "Bahnschrift SemiBold", Thin = "Bahnschrift Light";

    static FillSpec Rainbow(float angle = 0) => new()
    {
        Type = FillType.Linear, Color1 = "#FF2F7BFF", Color2 = "#FFE23FB8", Color3 = "#FFFF9A2E", ColorCount = 3, Angle = angle,
    };

    static FillSpec RingFill() => new()
    {
        Type = FillType.Linear, Color1 = "#FFFF9A2E", Color2 = "#FFE23FB8", Color3 = "#FF2F7BFF", ColorCount = 3, Angle = 60,
    };

    public static Theme Build(bool horizontal, bool dark)
    {
        var k = dark ? DarkLook : LightLook;
        var t = new Theme
        {
            Name = $"Rainbow {(dark ? "" : "Light ")}{(horizontal ? "Horizontal" : "Vertical")}",
            Width = horizontal ? 1920 : 462, Height = horizontal ? 462 : 1920,
            Background = Kit.Grad(k.Bg1, k.Bg2, 60),
        };
        var l = t.Elements;

        Element T(string name, string text, float x, float y, float w, float h, float size, string color, HAlign align = HAlign.Left, string font = Strong)
            => Kit.Txt(name, text, x, y, w, h, size, color, align, font);
        void Tile(string name, float x, float y, float w, float h) => l.Add(Kit.Plate(name, x, y, w, h, 6, Kit.Solid(k.Tile)));

        // ---- header: device name and clock ----
        void Header(float x, float y, float w, float h)
        {
            if (!horizontal)
            {
                float lw = w * 0.56f, rw = w - lw - 10;
                Tile("Device tile", x, y, lw, h);
                Tile("Clock tile", x + lw + 10, y, rw, h);
                l.Add(T("Device label", "Device Name:", x + 14, y + 16, lw - 20, 30, 22, k.Label, HAlign.Left, Thin));
                l.Add(T("Device name", "{host}", x + 14, y + 54, lw - 16, 56, 34, k.Text));
                l.Add(T("Status", "Status: Online", x + 14, y + 118, lw - 20, 34, 24, k.Blue, HAlign.Left, Thin));
                l.Add(T("Time", "{time:h:mm tt}", x + lw + 10, y + 26, rw - 14, 56, 36, k.Text, HAlign.Right));
                l.Add(T("Date", "{time:M/d/yyyy}", x + lw + 10, y + 96, rw - 14, 40, 22, k.Text, HAlign.Right, Thin));
            }
            else
            {
                float hw = (w - 10) / 2;
                Tile("Device tile", x, y, hw, h);
                Tile("Clock tile", x + hw + 10, y, hw, h);
                l.Add(T("Device label", "Device Name:", x + 14, y + 12, hw - 20, 28, 20, k.Label, HAlign.Left, Thin));
                l.Add(T("Device name", "{host}", x + 14, y + 42, hw - 20, 58, 42, k.Text));
                l.Add(T("Status", "Status: Online", x + 14, y + 112, hw - 20, 32, 22, k.Blue, HAlign.Left, Thin));
                l.Add(T("Time", "{time:h:mm tt}", x + hw + 10, y + 34, hw - 16, 64, 44, k.Text, HAlign.Right));
                l.Add(T("Date", "{time:M/d/yyyy}", x + hw + 10, y + 108, hw - 16, 40, 26, k.Text, HAlign.Right, Thin));
            }
        }

        // ---- CPU / GPU block ----
        void Chip(string key, string nameToken, string powerToken, string tempKey, string tempToken, string minToken, string maxToken,
            (string label, string value, string bind, float max)[] rows, float x, float y, float w, float h, bool wide = false)
        {
            Tile(key + " tile", x, y, w, h);
            l.Add(T(key + " name", nameToken, x + 16, y + 10, wide ? w - 32 : w - 150, wide ? 44 : 30, wide ? 30 : 20, k.Text));
            if (!wide) l.Add(T(key + " power", powerToken, x + w - 144, y + 10, 130, 30, 22, k.Text, HAlign.Right));

            float r = wide ? 170 : h >= 480 ? 176 : 150, rx = x + 14, ry = y + (wide ? 62 : 56);
            var ring = Kit.Ring(key + " ring", tempKey, rx, ry, r, RingFill(), k.Track, r * 0.1f, 100);
            ring.StartAngle = 120; ring.SweepAngle = 300;
            l.Add(ring);
            l.Add(T(key + " temp", tempToken, rx, ry + r * 0.33f, r, r * 0.34f, r * 0.2f, k.Text, HAlign.Center));

            if (wide) l.Add(T(key + " power", powerToken, rx - 6, ry + r + 2, r + 12, 44, 30, k.Text, HAlign.Center));
            float ax = rx + r + 24, aw = x + w - 16 - ax, rowH = r / rows.Length, rowSize = rows.Length > 4 ? 14 : 16;
            for (int i = 0; i < rows.Length; i++)
            {
                float y0 = ry + i * rowH;
                l.Add(T($"{key} {rows[i].label}", rows[i].label, ax, y0, aw * 0.5f, rowH * 0.6f, rowSize, k.Label, HAlign.Left, Thin));
                l.Add(T($"{key} {rows[i].label} value", rows[i].value, ax + aw * 0.3f, y0, aw * 0.7f, rowH * 0.6f, rowSize, k.Text, HAlign.Right));
                l.Add(Kit.Bar($"{key} {rows[i].label} bar", rows[i].bind, ax, y0 + rowH * 0.64f, aw, 6, Rainbow(), k.Track, 0, rows[i].max));
            }

            float gy = ry + r + (wide ? 54 : 16), gh = y + h - 14 - gy;
            l.Add(Kit.Plate(key + " graph frame", x + 14, gy, w - 28, gh, 2, null, k.Edge, 1.5f));
            var g = Kit.Widget(WidgetStyle.SimpleLine, key + " history", tempKey, x + 18, gy + 4, w - 36, gh - 8, Kit.Solid(k.Blue), k.Tile, 20, 100);
            g.Samples = 150; g.LineWidth = 2;
            l.Add(g);
            l.Add(T(key + " max", maxToken, x + 20, gy + 2, 60, 20, 12, k.Text, HAlign.Left, Thin));
            l.Add(T(key + " min", minToken, x + 20, gy + gh - 22, 60, 20, 12, k.Text, HAlign.Left, Thin));
        }

        // ---- memory block ----
        void Memory(float x, float y, float w, float h)
        {
            Tile("Memory tile", x, y, w, h);
            l.Add(T("Memory title", "Memory", x + 14, y + 8, w - 28, 36, 28, k.Text));
            l.Add(T("RAM speed label", "RAM Speed", x + 14, y + 48, w * 0.5f, 24, 14, k.Label, HAlign.Left, Thin));
            l.Add(T("RAM speed", "{ram.type}-{ram.speed:0}", x + w * 0.4f, y + 48, w * 0.6f - 14, 24, 15, k.Text, HAlign.Right));
            l.Add(T("Temps label", "Temps (°C):", x + 14, y + 74, w - 28, 24, 15, k.Label, HAlign.Left, Thin));

            float rs = Math.Min(96f, (w - 28 - 3 * 10) / 4f), extra = Math.Max(0, h - 330);
            float ry = y + 100 + extra * 0.25f;
            var temps = new[] { ("Board", "mobo.temp"), ("Chipset", "pch.temp"), ("GPU hot", "gpu.hotspot"), ("CPU", "cpu.temp") };
            for (int i = 0; i < 4; i++)
            {
                float rx = x + 14 + i * (rs + 10);
                var ring = Kit.Ring("Temp ring " + temps[i].Item1, temps[i].Item2, rx, ry, rs, RingFill(), k.Track, rs * 0.1f, 100);
                ring.StartAngle = 120; ring.SweepAngle = 300;
                l.Add(ring);
                l.Add(T("Temp " + temps[i].Item1, $"{{{temps[i].Item2}:0}}", rx, ry + rs * 0.3f, rs, rs * 0.4f, rs * 0.24f, k.Text, HAlign.Center));
                l.Add(T("Temp caption " + temps[i].Item1, temps[i].Item1, rx - 4, ry + rs + 2, rs + 8, 18, 11, k.Label, HAlign.Center, Thin));
            }

            float by = y + h - 82;
            l.Add(T("Memory label", "Memory", x + 14, by, w * 0.5f, 26, 16, k.Label, HAlign.Left, Thin));
            l.Add(T("Memory pct", "{ram.load:0}%", x + w * 0.5f - 14, by, w * 0.5f, 26, 16, k.Text, HAlign.Right));
            l.Add(Kit.Bar("Memory bar", "ram.load", x + 14, by + 30, w - 28, 8, Rainbow(), k.Track));
            l.Add(T("Memory used", "U: {ram.used:0.0} GB", x + 14, by + 46, w * 0.5f, 26, 15, k.Text, HAlign.Left, Thin));
            l.Add(T("Memory free", "F: {ram.free:0.0} GB", x + w * 0.5f - 14, by + 46, w * 0.5f, 26, 15, k.Text, HAlign.Right, Thin));
        }

        // ---- storage block ----
        void Storage(float x, float y, float w, float h)
        {
            Tile("Storage tile", x, y, w, h);
            l.Add(T("Storage title", "Storage", x + 14, y + 8, w - 28, 36, 28, k.Text));
            float rowH = (h - 62) / 4, size = rowH > 84 ? 20 : 17;
            int n = 0;
            foreach (char d in new[] { 'c', 'd', 'e', 'g' })
            {
                float y0 = y + 56 + n++ * rowH;
                string up = char.ToUpperInvariant(d).ToString();
                l.Add(T($"Drive {up}", $"{up}:  {{drive.{d}.used:0}}%", x + 14, y0, w * 0.45f, 28, size, k.Text, HAlign.Left, Thin));
                l.Add(T($"Drive {up} free", $"Free: {{drive.{d}.freegb:0}} GB", x + w * 0.4f, y0, w * 0.6f - 14, 28, size, k.Text, HAlign.Right, Thin));
                l.Add(Kit.Bar($"Drive {up} bar", $"drive.{d}.used", x + 14, y0 + size + 14, w - 28, 8, Rainbow(), k.Track));
            }
        }

        var cpuRows = new[]
        {
            ("Load", "{cpu.load:0}%", "cpu.load", 100f), ("Voltage", "{cpu.voltage:0.000} V", "cpu.voltage", 1.6f),
            ("CPU Clock", "{cpu.clock:0.00} GHz", "cpu.clock", 6f), ("Fan", "{cpu.fan:0} RPM", "cpu.fan", 3000f),
        };
        var gpuRows = new[]
        {
            ("Load", "{gpu.load:0}%", "gpu.load", 100f), ("Voltage", "{gpu.voltage:0.000} V", "gpu.voltage", 1.3f),
            ("GPU Clock", "{gpu.clockmhz:0} MHz", "gpu.clockmhz", 3000f), ("VRAM", "{gpu.vram:0.0} GB", "gpu.vrampct", 100f),
        };
        var gpuRowsWide = new[]
        {
            ("Load", "{gpu.load:0}%", "gpu.load", 100f), ("Voltage", "{gpu.voltage:0.000} V", "gpu.voltage", 1.3f),
            ("GPU Clock", "{gpu.clockmhz:0} MHz", "gpu.clockmhz", 3000f), ("Hot spot", "{gpu.hotspot:0}{unit}", "gpu.hotspot", 110f),
            ("VRAM", "{gpu.vram:0.0} GB", "gpu.vrampct", 100f), ("Fan", "{gpu.fan:0} RPM", "gpu.fan", 3000f),
        };

        if (!horizontal)
        {
            const float X = 10, W = 442, G = 12;
            float y = 8;
            Header(X, y, W, 196); y += 196 + G;
            Chip("CPU", "{cpu.name}", "{cpu.power:0.00} W", "cpu.temp", "{cpu.temp:0}{unit}", "{cpu.tempmin:0}", "{cpu.tempmax:0}", cpuRows, X, y, W, 500); y += 500 + G;
            Chip("GPU", "{gpu.name}", "{gpu.power:0.00} W", "gpu.temp", "{gpu.temp:0}{unit}", "{gpu.tempmin:0}", "{gpu.tempmax:0}", gpuRows, X, y, W, 500); y += 500 + G;
            Memory(X, y, W, 330); y += 330 + G;
            Storage(X, y, W, 330);
        }
        else
        {
            const float Y = 8, H = 446, G = 10;
            float x = 10;
            Chip("CPU", "{cpu.name}", "{cpu.power:0.00} W", "cpu.temp", "{cpu.temp:0}{unit}", "{cpu.tempmin:0}", "{cpu.tempmax:0}", cpuRows, x, Y, 630, H, wide: true); x += 630 + G;
            Chip("GPU", "{gpu.name}", "{gpu.power:0.00} W", "gpu.temp", "{gpu.temp:0}{unit}", "{gpu.tempmin:0}", "{gpu.tempmax:0}", gpuRowsWide, x, Y, 610, H, wide: true); x += 610 + G;
            float rw = 1910 - x, half = (rw - G) / 2, top = 172;
            Header(x, Y, rw, top);
            Memory(x, Y + top + G, half, H - top - G);
            Storage(x + half + G, Y + top + G, half, H - top - G);
        }
        return t;
    }
}
