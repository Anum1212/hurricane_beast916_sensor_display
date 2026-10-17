using System.Diagnostics;

namespace HurricaneBeastPanel;

/// <summary>
/// The sensor library reads CPU temperature and power through the free PawnIO driver (https://pawnio.eu).
/// This checks whether it is installed and can install it with winget (the Windows package manager) or open its website.
/// </summary>
static class PawnIoSetup
{
    public const string Site = "https://pawnio.eu";
    public const string WingetId = "namazso.PawnIO";

    public static bool IsInstalled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
            return key != null;
        }
        catch { return true; } // cannot tell, so do not nag
    }

    public static bool HasWinget()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("winget", "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            return p != null && p.WaitForExit(8000) && p.ExitCode == 0;
        }
        catch { return false; }
    }

    public static void OpenSite()
    {
        try { Process.Start(new ProcessStartInfo(Site) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Could not open " + Site + ": " + ex.Message); }
    }

    /// <summary>Installs PawnIO with winget. Returns an error message, or null when it worked.</summary>
    public static async Task<string?> InstallAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("winget",
                    $"install --id {WingetId} -e --silent --accept-package-agreements --accept-source-agreements")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
                if (p == null) return "Could not start winget.";
                string output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(240_000)) { try { p.Kill(); } catch { } return "The installer took too long."; }
                Log.Write("PawnIO install exit code " + p.ExitCode);
                return p.ExitCode == 0 || IsInstalled() ? null : "winget could not install PawnIO (code " + p.ExitCode + ").";
            }
            catch (Exception ex) { return ex.Message; }
        });
    }
}
