using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using LibreHardwareMonitor.Hardware;

namespace HurricaneBeastPanel;

public sealed class SensorSnapshot
{
    public string CpuName = "CPU";
    public string GpuName = "GPU";
    public float? CpuTemp, CpuLoad, CpuPower, CpuClockMhz;
    public float? GpuTemp, GpuLoad, GpuPower, GpuClockMhz, GpuFan, GpuMemUsedMb, GpuMemTotalMb;
    public float? RamLoad, RamUsedGb, RamTotalGb;
    public float? CpuFan, PumpFan, CaseFan;
    public float NetUpBps, NetDownBps;
    public float? MoboTemp, PchTemp, GpuMemClockMhz;
    public string BoardName = "", BiosVersion = "", RamType = "";
    public int RamSpeedMts;
    public List<DriveSnap> Drives = new();
    public float? GpuHotspot, CpuVoltage, GpuVoltage, NetSentGb, NetRecvGb;
    public string NetIp = "", NetName = "", DisplayRes = "";
    public int ProcessCount, DisplayHz;
    public WeatherInfo? Weather;
}

public sealed class DriveSnap
{
    public char Letter;
    public string Label = "";
    public float TotalGb, UsedGb;
    public float FreeGb => TotalGb - UsedGb;
    public float UsedPct => TotalGb > 0 ? UsedGb / TotalGb * 100f : 0f;
}

/// <summary>Reads CPU/GPU/RAM/fan/network sensors through LibreHardwareMonitor. Needs admin rights for CPU temperature and power.</summary>
public sealed class SensorService : IDisposable
{
    readonly Computer _pc;

    public string? OpenError { get; }

    static List<DriveSnap> _drives = new();
    static long _drivesTick;

    /// <summary>Fixed drives with their usage, re-read every 15 seconds (reading wakes sleeping disks).</summary>
    static List<DriveSnap> ReadDrives()
    {
        if (_drives.Count > 0 && Environment.TickCount64 - _drivesTick < 15000) return _drives;
        _drivesTick = Environment.TickCount64;
        try
        {
            _drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => new DriveSnap
                {
                    Letter = char.ToUpperInvariant(d.Name[0]), Label = d.VolumeLabel,
                    TotalGb = d.TotalSize / 1073741824f, UsedGb = (d.TotalSize - d.TotalFreeSpace) / 1073741824f,
                }).ToList();
        }
        catch { /* keep the previous list */ }
        return _drives;
    }

    // Slow-changing facts are read now and then instead of on every poll.
    static string _ip = "", _ipName = "";
    static long _ipTick;
    static int _processes;
    static long _processTick;
    static string _resolution = "";
    static int _refreshHz;
    static long _displayTick;

    static void ReadSlowFacts(SensorSnapshot s)
    {
        long now = Environment.TickCount64;
        if (now - _ipTick > 15000 || _ipTick == 0)
        {
            _ipTick = now;
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                    .Select(n => (n, props: n.GetIPProperties()))
                    .FirstOrDefault(x => x.props.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));
                if (nic.n != null)
                {
                    _ipName = nic.n.Name;
                    _ip = nic.props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString() ?? "";
                }
            }
            catch { }
        }
        if (now - _processTick > 10000 || _processTick == 0)
        {
            _processTick = now;
            try
            {
                var all = Process.GetProcesses();
                _processes = all.Length;
                foreach (var p in all) p.Dispose();
            }
            catch { }
        }
        if (now - _displayTick > 60000 || _displayTick == 0)
        {
            _displayTick = now;
            try
            {
                var b = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
                _resolution = b.Width > 0 ? $"{b.Width} x {b.Height}" : "";
                using var searcher = new ManagementObjectSearcher("SELECT CurrentRefreshRate FROM Win32_VideoController");
                _refreshHz = searcher.Get().Cast<ManagementObject>().Select(m => Convert.ToInt32(m["CurrentRefreshRate"] ?? 0)).DefaultIfEmpty(0).Max();
            }
            catch { }
        }
        s.NetIp = _ip;
        s.NetName = _ipName;
        s.ProcessCount = _processes;
        s.DisplayRes = _resolution;
        s.DisplayHz = _refreshHz;
    }

    public SensorService()
    {
        SystemInfo.EnsureLoaded();
        _pc = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = true,
        };
        try { _pc.Open(); }
        catch (Exception ex) { OpenError = ex.Message; }
    }

    static IEnumerable<IHardware> Walk(IEnumerable<IHardware> hardware)
    {
        foreach (var h in hardware)
        {
            yield return h;
            foreach (var sub in Walk(h.SubHardware)) yield return sub;
        }
    }

    static IEnumerable<ISensor> SensorsOf(IHardware? h, SensorType type) =>
        h == null
            ? Enumerable.Empty<ISensor>()
            : Walk(new[] { h }).SelectMany(x => x.Sensors).Where(s => s.SensorType == type && s.Value.HasValue);

    /// <summary>First sensor matching one of the names (in priority order), optionally falling back to any sensor of that type.</summary>
    static float? Pick(IHardware? h, SensorType type, bool fallbackAny, params string[] names)
    {
        var list = SensorsOf(h, type).ToList();
        foreach (string n in names)
        {
            var m = list.FirstOrDefault(s => s.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
            if (m != null) return m.Value;
        }
        return fallbackAny ? list.FirstOrDefault()?.Value : null;
    }

    static string Clean(string name) =>
        name.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "").Replace("  ", " ").Trim();

    public SensorSnapshot Poll()
    {
        var all = Walk(_pc.Hardware).ToList();
        foreach (var h in all)
        {
            try { h.Update(); } catch { /* a misbehaving sensor must not stop the others */ }
        }

        var s = new SensorSnapshot();

        var cpu = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu != null)
        {
            s.CpuName = Clean(cpu.Name);
            s.CpuTemp = Pick(cpu, SensorType.Temperature, true,
                "CPU Package", "Core (Tctl/Tdie)", "Tctl/Tdie", "Core Max", "Core Average");
            s.CpuLoad = Pick(cpu, SensorType.Load, false, "CPU Total");
            s.CpuPower = Pick(cpu, SensorType.Power, false, "CPU Package", "Package");
            var clocks = SensorsOf(cpu, SensorType.Clock)
                .Where(c => c.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                            && !c.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value!.Value).ToList();
            if (clocks.Count > 0) s.CpuClockMhz = clocks.Max();
        }

        var gpu = all.FirstOrDefault(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                  ?? all.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel);
        if (gpu != null)
        {
            s.GpuName = Clean(gpu.Name);
            s.GpuTemp = Pick(gpu, SensorType.Temperature, true, "GPU Core", "GPU Hot Spot");
            s.GpuLoad = Pick(gpu, SensorType.Load, false, "GPU Core");
            s.GpuPower = Pick(gpu, SensorType.Power, true,
                "GPU Package", "GPU Total Board Power", "GPU Power");
            s.GpuClockMhz = Pick(gpu, SensorType.Clock, false, "GPU Core");
            s.GpuFan = SensorsOf(gpu, SensorType.Fan).FirstOrDefault()?.Value;
            s.GpuMemUsedMb = Pick(gpu, SensorType.SmallData, false, "GPU Memory Used", "D3D Dedicated Memory Used");
            s.GpuMemTotalMb = Pick(gpu, SensorType.SmallData, false, "GPU Memory Total", "D3D Dedicated Memory Total");
        }

        var mem = all.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        if (mem != null)
        {
            s.RamLoad = Pick(mem, SensorType.Load, false, "Memory");
            float? used = Pick(mem, SensorType.Data, false, "Memory Used");
            float? avail = Pick(mem, SensorType.Data, false, "Memory Available");
            s.RamUsedGb = used;
            if (used.HasValue && avail.HasValue) s.RamTotalGb = used + avail;
        }

        var fans = all
            .Where(h => h.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO
                or HardwareType.EmbeddedController or HardwareType.Cooler)
            .SelectMany(h => h.Sensors)
            .Where(x => x.SensorType == SensorType.Fan && x.Value is > 0)
            .ToList();
        s.CpuFan = fans.FirstOrDefault(f => f.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                                            && !f.Name.Contains("OPT", StringComparison.OrdinalIgnoreCase))?.Value;
        s.PumpFan = fans.FirstOrDefault(f => f.Name.Contains("pump", StringComparison.OrdinalIgnoreCase)
                                             || f.Name.Contains("AIO", StringComparison.OrdinalIgnoreCase))?.Value;
        s.CaseFan = fans
            .Where(f => f.Value != s.CpuFan && f.Value != s.PumpFan)
            .Select(f => f.Value).Max();

        s.GpuMemClockMhz = gpu == null ? null : Pick(gpu, SensorType.Clock, false, "GPU Memory");

        // Motherboard and chipset temperatures come from the board's monitoring chip (needs administrator rights).
        var boardTemps = all
            .Where(h => h.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.EmbeddedController)
            .SelectMany(h => h.Sensors)
            .Where(x => x.SensorType == SensorType.Temperature && x.Value is > 0 and < 120)
            .ToList();
        ISensor? Named(params string[] parts) =>
            boardTemps.FirstOrDefault(x => parts.Any(p => x.Name.Contains(p, StringComparison.OrdinalIgnoreCase)));
        var pch = Named("PCH", "Chipset");
        s.PchTemp = pch?.Value;
        s.MoboTemp = (Named("Motherboard", "Mainboard", "System", "MB") ??
                      boardTemps.FirstOrDefault(x => x != pch && !x.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)))?.Value;

        s.GpuHotspot = gpu == null ? null : Pick(gpu, SensorType.Temperature, false, "GPU Hot Spot", "GPU Hotspot");
        s.CpuVoltage = cpu == null ? null : Pick(cpu, SensorType.Voltage, true, "CPU Core", "Core (SVI2 TFN)", "VCore");
        s.GpuVoltage = gpu == null ? null : Pick(gpu, SensorType.Voltage, false, "GPU Core");
        ReadSlowFacts(s);

        s.BoardName = SystemInfo.BoardName;
        s.BiosVersion = SystemInfo.BiosVersion;
        s.RamType = SystemInfo.RamType;
        s.RamSpeedMts = SystemInfo.RamSpeedMts;
        s.Drives = ReadDrives();
        s.Weather = WeatherService.Current;

        // Use the busiest adapter so VPN/virtual adapters are not double counted.
        foreach (var nic in all.Where(h => h.HardwareType == HardwareType.Network))
        {
            float up = Pick(nic, SensorType.Throughput, false, "Upload Speed") ?? 0;
            float down = Pick(nic, SensorType.Throughput, false, "Download Speed") ?? 0;
            if (up + down > s.NetUpBps + s.NetDownBps)
            {
                s.NetUpBps = up;
                s.NetDownBps = down;
                s.NetSentGb = Pick(nic, SensorType.Data, false, "Data Uploaded");
                s.NetRecvGb = Pick(nic, SensorType.Data, false, "Data Downloaded");
            }
        }

        return s;
    }

    public void Dispose()
    {
        try { _pc.Close(); } catch { }
    }
}
