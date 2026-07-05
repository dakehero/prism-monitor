using System.Diagnostics;
using System.Security.Principal;

namespace PrismMonitor.App.Elevation;

internal static class ElevationHelper
{
    public static bool IsCurrentProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool TryRelaunchCurrentProcessAsAdministrator()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = processPath,
                UseShellExecute = true,
                Verb = "runas"
            };

            _ = Process.Start(startInfo);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
