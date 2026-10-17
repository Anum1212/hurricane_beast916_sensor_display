using System.Diagnostics;
using System.Security.Principal;

namespace HurricaneBeastPanel;

/// <summary>
/// Start-with-Windows through a scheduled task. A task with the highest run level starts the app elevated
/// without a UAC prompt, which LibreHardwareMonitor needs for CPU temperature and power.
/// </summary>
static class StartupTask
{
    const string TaskName = "HurricaneBeastPanel";

    public static bool IsEnabled() => Run($"/Query /TN {TaskName}") == 0;

    public static bool Enable(string exePath)
    {
        string user = WindowsIdentity.GetCurrent().Name;
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{System.Security.SecurityElement.Escape(user)}</UserId>
                  <Delay>PT10S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{System.Security.SecurityElement.Escape(user)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{System.Security.SecurityElement.Escape(exePath)}</Command>
                  <Arguments>--minimized</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
        string tmp = Path.Combine(Path.GetTempPath(), "HurricaneBeastPanel_task.xml");
        File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
        try { return Run($"/Create /TN {TaskName} /XML \"{tmp}\" /F") == 0; }
        finally { try { File.Delete(tmp); } catch { } }
    }

    public static bool Disable() => Run($"/Delete /TN {TaskName} /F") == 0;

    static int Run(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            p.WaitForExit(10000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch { return -1; }
    }
}
