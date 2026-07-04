using System.ComponentModel;
using System.Diagnostics;

namespace PrismMonitor.App.Processes;

internal sealed record ProcessTerminationResult(bool Succeeded, string Message);

internal sealed class ProcessTerminator
{
    public ProcessTerminationResult Terminate(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            process.Kill();
            return new ProcessTerminationResult(true, "End requested");
        }
        catch (ArgumentException)
        {
            return new ProcessTerminationResult(false, "Process has exited");
        }
        catch (InvalidOperationException)
        {
            return new ProcessTerminationResult(false, "Process has exited");
        }
        catch (Win32Exception ex)
        {
            return new ProcessTerminationResult(false, $"Could not end process: {ex.Message}");
        }
        catch (NotSupportedException)
        {
            return new ProcessTerminationResult(false, "Ending this process is not supported");
        }
    }
}
