using System.Globalization;
using System.Text.RegularExpressions;

namespace HurricaneBeastPanel;

public sealed record TokenInfo(string Key, string Description, bool Numeric, string DefaultFormat = "0");

/// <summary>
/// Placeholders usable in theme text, e.g. "{cpu.temp}" or "{cpu.clock:0.0}" or "{time:HH:mm:ss}".
/// Numeric tokens can also drive a shape's width (bars).
/// </summary>
public static class Tokens
{
    public static readonly TokenInfo[] All =
    {
        new("time", "Time (format e.g. HH:mm:ss)", false),
        new("date", "Date (format e.g. dd/MM/yyyy)", false),
        new("year", "Year (2026)", false),
        new("year2", "Year, 2 digits (26)", false),
        new("month", "Month number (4)", false),
        new("month2", "Month number, 2 digits (04)", false),
        new("monthshort", "Month name, short (Apr)", false),
        new("monthname", "Month name, full (April)", false),
        new("day", "Day of month (7)", false),
        new("day2", "Day of month, 2 digits (07)", false),
        new("weekdayshort", "Weekday, short (Tue)", false),
        new("weekday", "Weekday, full (Tuesday)", false),
        new("dayofyear", "Day of the year (107)", false),
        new("week", "ISO week number (15)", false),
        new("hour24", "Hour, 24-hour (9 or 21)", false),
        new("hour24_2", "Hour, 24-hour, 2 digits (09)", false),
        new("hour12", "Hour, 12-hour (9)", false),
        new("hour12_2", "Hour, 12-hour, 2 digits (09)", false),
        new("minute", "Minute (5)", false),
        new("minute2", "Minute, 2 digits (05)", false),
        new("second", "Second (7)", false),
        new("second2", "Second, 2 digits (07)", false),
        new("ampm", "AM / PM", false),
        new("uptime", "System uptime (hours:minutes:seconds)", false),
        new("uptime.days", "System uptime (3d 09:20)", false),
        new("net.ip", "Local IP address", false),
        new("net.name", "Network adapter name (Wi-Fi, Ethernet)", false),
        new("display.res", "Main display resolution", false),
        new("display.hz", "Main display refresh rate Hz", true),
        new("sys.processes", "Number of running processes", true),
        new("host", "Computer name", false),
        new("unit", "Temperature unit (°C/°F)", false),
        new("cpu.name", "CPU model", false),
        new("cpu.temp", "CPU temperature", true),
        new("cpu.load", "CPU load %", true),
        new("cpu.power", "CPU power W", true),
        new("cpu.clock", "CPU clock GHz", true, "0.00"),
        new("cpu.clockmhz", "CPU clock MHz", true),
        new("cpu.fan", "CPU fan RPM", true),
        new("gpu.name", "GPU model", false),
        new("gpu.temp", "GPU temperature", true),
        new("gpu.load", "GPU load %", true),
        new("gpu.power", "GPU power W", true),
        new("gpu.clock", "GPU clock GHz", true, "0.00"),
        new("gpu.clockmhz", "GPU clock MHz", true),
        new("gpu.memclock", "GPU memory clock MHz", true),
        new("gpu.fan", "GPU fan RPM", true),
        new("gpu.hotspot", "GPU hot spot temperature", true),
        new("gpu.voltage", "GPU voltage V", true, "0.000"),
        new("cpu.voltage", "CPU voltage V", true, "0.000"),
        new("cpu.tempmin", "CPU temperature, lowest since start", true),
        new("cpu.tempmax", "CPU temperature, highest since start", true),
        new("gpu.tempmin", "GPU temperature, lowest since start", true),
        new("gpu.tempmax", "GPU temperature, highest since start", true),
        new("mobo.tempmin", "Motherboard temperature, lowest since start", true),
        new("mobo.tempmax", "Motherboard temperature, highest since start", true),
        new("gpu.vram", "GPU memory used GB", true, "0.0"),
        new("gpu.vramtotal", "GPU memory total GB", true, "0.0"),
        new("gpu.vrampct", "GPU memory used %", true),
        new("ram.load", "RAM used %", true),
        new("ram.used", "RAM used GB", true, "0.0"),
        new("ram.total", "RAM total GB", true, "0.0"),
        new("ram.usedmb", "RAM used MB", true),
        new("ram.freemb", "RAM free MB", true),
        new("ram.speed", "RAM speed MT/s", true),
        new("ram.clock", "RAM clock MHz (speed / 2)", true),
        new("ram.type", "RAM type (DDR4, DDR5)", false),
        new("mobo.name", "Motherboard name", false),
        new("bios.version", "BIOS version", false),
        new("mobo.temp", "Motherboard temperature", true),
        new("pch.temp", "Chipset (PCH) temperature", true),
        new("drive.c.used", "Drive C used %", true),
        new("drive.d.used", "Drive D used %", true),
        new("drive.e.used", "Drive E used %", true),
        new("drive.f.used", "Drive F used %", true),
        new("drive.g.used", "Drive G used %", true),
        new("drive.c.freegb", "Drive C free GB", true),
        new("drive.d.freegb", "Drive D free GB", true),
        new("drive.e.freegb", "Drive E free GB", true),
        new("drive.g.freegb", "Drive G free GB", true),
        new("drive.c.label", "Drive C name (also .totalgb, .usedgb; any letter)", false),
        new("pump.fan", "Pump RPM", true),
        new("case.fan", "Case fan RPM", true),
        new("net.up", "Upload Mbps", true, "0.0"),
        new("net.down", "Download Mbps", true, "0.0"),
        new("net.upmbs", "Upload MB/s", true, "0.0"),
        new("net.downmbs", "Download MB/s", true, "0.0"),
        new("net.sentgb", "Total uploaded GB", true, "0.0"),
        new("net.recvgb", "Total downloaded GB", true, "0.0"),
    };

    public static readonly string[] NumericKeys = All.Where(t => t.Numeric).Select(t => t.Key).ToArray();

    /// <summary>Tokens whose history is recorded for graphs (the min/max ones are derived from that history).</summary>
    public static readonly string[] HistoryKeys = NumericKeys.Where(k => !k.EndsWith("min") && !k.EndsWith("max")).ToArray();

    static readonly Regex Pattern = new(@"\{([A-Za-z0-9_.]+)(?::([^}]*))?\}", RegexOptions.Compiled);

    public static string Expand(string text, SensorSnapshot s, AppSettings settings)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('{')) return text;
        return Pattern.Replace(text, m =>
        {
            string key = m.Groups[1].Value.ToLowerInvariant();
            string? fmt = m.Groups[2].Success ? m.Groups[2].Value : null;
            return Resolve(key, fmt, s, settings) ?? m.Value;
        });
    }

    static string? Resolve(string key, string? fmt, SensorSnapshot s, AppSettings settings)
    {
        var now = DateTime.Now;
        switch (key)
        {
            case "time": return Safe(() => now.ToString(CustomFormat(fmt ?? "HH:mm"), CultureInfo.CurrentCulture));
            case "date": return Safe(() => now.ToString(CustomFormat(fmt ?? "dddd, MMMM d"), CultureInfo.CurrentCulture));
            case "year": return now.ToString("yyyy", CultureInfo.CurrentCulture);
            case "year2": return now.ToString("yy", CultureInfo.CurrentCulture);
            case "month": return now.ToString("%M", CultureInfo.CurrentCulture);
            case "month2": return now.ToString("MM", CultureInfo.CurrentCulture);
            case "monthshort": return now.ToString("MMM", CultureInfo.CurrentCulture);
            case "monthname": return now.ToString("MMMM", CultureInfo.CurrentCulture);
            case "day": return now.ToString("%d", CultureInfo.CurrentCulture);
            case "day2": return now.ToString("dd", CultureInfo.CurrentCulture);
            case "weekdayshort": return now.ToString("ddd", CultureInfo.CurrentCulture);
            case "weekday": return now.ToString("dddd", CultureInfo.CurrentCulture);
            case "dayofyear": return now.DayOfYear.ToString(fmt ?? "0", CultureInfo.InvariantCulture);
            case "week": return ISOWeek.GetWeekOfYear(now).ToString(fmt ?? "0", CultureInfo.InvariantCulture);
            case "hour24": return now.ToString("%H", CultureInfo.CurrentCulture);
            case "hour24_2": return now.ToString("HH", CultureInfo.CurrentCulture);
            case "hour12": return now.ToString("%h", CultureInfo.CurrentCulture);
            case "hour12_2": return now.ToString("hh", CultureInfo.CurrentCulture);
            case "minute": return now.ToString("%m", CultureInfo.CurrentCulture);
            case "minute2": return now.ToString("mm", CultureInfo.CurrentCulture);
            case "second": return now.ToString("%s", CultureInfo.CurrentCulture);
            case "second2": return now.ToString("ss", CultureInfo.CurrentCulture);
            case "ampm": return now.ToString("tt", CultureInfo.CurrentCulture);
            case "host": return Environment.MachineName;
            case "net.ip": return s.NetIp.Length > 0 ? s.NetIp : "--";
            case "net.name": return s.NetName.Length > 0 ? s.NetName : "--";
            case "display.res": return s.DisplayRes.Length > 0 ? s.DisplayRes : "--";
            case "uptime":
            {
                var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return $"{(int)up.TotalHours:00}:{up.Minutes:00}:{up.Seconds:00}";
            }
            case "uptime.days":
            {
                var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return up.Days > 0 ? $"{up.Days}d {up.Hours:00}:{up.Minutes:00}" : $"{up.Hours:00}:{up.Minutes:00}:{up.Seconds:00}";
            }
            case "unit": return settings.Fahrenheit ? "°F" : "°C";
            case "cpu.name": return s.CpuName;
            case "gpu.name": return s.GpuName;
            case "mobo.name": return s.BoardName.Length > 0 ? s.BoardName : "--";
            case "bios.version": return s.BiosVersion.Length > 0 ? s.BiosVersion : "--";
            case "ram.type": return s.RamType.Length > 0 ? s.RamType : "--";
        }
        if (key.StartsWith("drive.") && key.EndsWith(".label"))
        {
            var d = DriveFor(key, s);
            return d == null ? "--" : d.Label.Length > 0 ? $"{d.Letter}: {d.Label}" : d.Letter + ":";
        }

        // Drive values work for any letter (drive.h.used, drive.h.totalgb, ...) even if not listed in the menu.
        var info = All.FirstOrDefault(t => t.Key == key && t.Numeric) ?? (key.StartsWith("drive.") ? new TokenInfo(key, "", true) : null);
        if (info == null) return null;
        float? v = Number(key, s);
        if (v == null) return "--";
        if (key.EndsWith(".temp") && settings.Fahrenheit) v = v * 9 / 5 + 32;
        return Safe(() => v.Value.ToString(fmt ?? info.DefaultFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>A single-letter format such as "M" or "d" is a standard .NET format (October 17, short date, ...). Prefix it with % so it means the plain part.</summary>
    static string CustomFormat(string fmt) => fmt.Length == 1 ? "%" + fmt : fmt;

    static string Safe(Func<string> f)
    {
        try { return f(); } catch (FormatException) { return "?"; }
    }

    /// <summary>The drive a token like "drive.d.used" refers to, or null if that drive is not present.</summary>
    static DriveSnap? DriveFor(string key, SensorSnapshot s)
    {
        var parts = key.Split('.');
        return parts.Length >= 3 && parts[1].Length == 1
            ? s.Drives.FirstOrDefault(d => char.ToLowerInvariant(d.Letter) == parts[1][0])
            : null;
    }

    static float? DriveNumber(string key, SensorSnapshot s)
    {
        var d = DriveFor(key, s);
        if (d == null) return null;
        return key.Split('.')[2] switch
        {
            "used" => d.UsedPct, "usedgb" => d.UsedGb, "freegb" => d.FreeGb, "totalgb" => d.TotalGb, _ => null,
        };
    }

    /// <summary>Raw value of a numeric token (temperatures in °C).</summary>
    public static float? Number(string key, SensorSnapshot s) => key switch
    {
        "cpu.temp" => s.CpuTemp,
        "cpu.load" => s.CpuLoad,
        "cpu.power" => s.CpuPower,
        "cpu.clock" => s.CpuClockMhz / 1000,
        "cpu.clockmhz" => s.CpuClockMhz,
        "gpu.hotspot" => s.GpuHotspot,
        "gpu.voltage" => s.GpuVoltage,
        "cpu.voltage" => s.CpuVoltage,
        "display.hz" => s.DisplayHz > 0 ? s.DisplayHz : null,
        "sys.processes" => s.ProcessCount > 0 ? s.ProcessCount : null,
        "net.upmbs" => s.NetUpBps / 1_000_000f,
        "net.downmbs" => s.NetDownBps / 1_000_000f,
        "net.sentgb" => s.NetSentGb,
        "net.recvgb" => s.NetRecvGb,
        var k when k.EndsWith("min") && k.Contains(".temp") => SensorHistory.Min(k[..^3]),
        var k when k.EndsWith("max") && k.Contains(".temp") => SensorHistory.Max(k[..^3]),
        "gpu.clockmhz" => s.GpuClockMhz,
        "gpu.memclock" => s.GpuMemClockMhz,
        "mobo.temp" => s.MoboTemp,
        "pch.temp" => s.PchTemp,
        "ram.usedmb" => s.RamUsedGb * 1024,
        "ram.freemb" => (s.RamTotalGb - s.RamUsedGb) * 1024,
        "ram.speed" => s.RamSpeedMts > 0 ? s.RamSpeedMts : null,
        "ram.clock" => s.RamSpeedMts > 0 ? s.RamSpeedMts / 2f : null,
        var k when k.StartsWith("drive.") => DriveNumber(k, s),
        "cpu.fan" => s.CpuFan,
        "gpu.temp" => s.GpuTemp,
        "gpu.load" => s.GpuLoad,
        "gpu.power" => s.GpuPower,
        "gpu.clock" => s.GpuClockMhz / 1000,
        "gpu.fan" => s.GpuFan,
        "gpu.vram" => s.GpuMemUsedMb / 1024,
        "gpu.vramtotal" => s.GpuMemTotalMb / 1024,
        "gpu.vrampct" => s.GpuMemTotalMb is > 0 ? s.GpuMemUsedMb / s.GpuMemTotalMb * 100 : null,
        "ram.load" => s.RamLoad,
        "ram.used" => s.RamUsedGb,
        "ram.total" => s.RamTotalGb,
        "pump.fan" => s.PumpFan,
        "case.fan" => s.CaseFan,
        "net.up" => s.NetUpBps * 8 / 1_000_000,
        "net.down" => s.NetDownBps * 8 / 1_000_000,
        _ => null,
    };
}
