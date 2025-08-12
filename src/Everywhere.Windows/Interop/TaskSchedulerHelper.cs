using System.Diagnostics;

namespace Everywhere.Windows.Interop;

public static class TaskSchedulerHelper
{
    public static bool IsTaskScheduled(string taskName)
    {
        using var process = Process.Start(
            new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{taskName}\"")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
        if (process is null) return false;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    public static void CreateScheduledTask(string taskName, string appPath)
    {
        Process.Start(
            new ProcessStartInfo("schtasks.exe", $"/Create /TN \"{taskName}\" /TR \"{appPath.Replace("\"", "\\\"")}\" /SC ONLOGON /RL HIGHEST /F")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })?.WaitForExit();
    }

    public static void DeleteScheduledTask(string taskName)
    {
        Process.Start(
            new ProcessStartInfo("schtasks.exe", $"/Delete /TN \"{taskName}\" /F")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })?.WaitForExit();
    }
}