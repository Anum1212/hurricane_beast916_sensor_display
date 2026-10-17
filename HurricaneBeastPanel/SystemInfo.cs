using System.Management;
using Microsoft.Win32;

namespace HurricaneBeastPanel;

/// <summary>Facts about the PC that do not change while the app runs: motherboard, BIOS and memory type/speed.</summary>
static class SystemInfo
{
    static readonly object Lock = new();
    static bool _started;

    public static string BoardName { get; private set; } = "";
    public static string BiosVersion { get; private set; } = "";
    public static string RamType { get; private set; } = "";
    public static int RamSpeedMts { get; private set; }

    /// <summary>Starts reading in the background (WMI can take a moment). Values appear in snapshots as soon as they are known.</summary>
    public static void EnsureLoaded()
    {
        lock (Lock)
        {
            if (_started) return;
            _started = true;
        }
        Task.Run(Load);
    }

    static void Load()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            if (key != null)
            {
                string product = key.GetValue("BaseBoardProduct") as string ?? "";
                string maker = key.GetValue("BaseBoardManufacturer") as string ?? "";
                BoardName = product.Length > 0 ? product : (key.GetValue("SystemProductName") as string ?? "");
                if (maker.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase) && !BoardName.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase))
                    BoardName = "Gigabyte " + BoardName;
                else if (maker.Contains("ASUS", StringComparison.OrdinalIgnoreCase) && !BoardName.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
                    BoardName = "ASUS " + BoardName;
                BiosVersion = (key.GetValue("BIOSVersion") as string ?? "").Trim();
            }
        }
        catch (Exception ex) { Log.Write("Board info failed: " + ex.Message); }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Speed, ConfiguredClockSpeed, SMBIOSMemoryType, MemoryType FROM Win32_PhysicalMemory");
            foreach (ManagementObject m in searcher.Get())
            {
                int configured = Convert.ToInt32(m["ConfiguredClockSpeed"] ?? 0), rated = Convert.ToInt32(m["Speed"] ?? 0);
                RamSpeedMts = configured > 0 ? configured : rated;
                int type = Convert.ToInt32(m["SMBIOSMemoryType"] ?? 0);
                RamType = type switch
                {
                    20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", 34 => "DDR5", 30 => "LPDDR4", 35 => "LPDDR5", _ => "",
                };
                break;
            }
        }
        catch (Exception ex) { Log.Write("Memory info failed: " + ex.Message); }
    }
}