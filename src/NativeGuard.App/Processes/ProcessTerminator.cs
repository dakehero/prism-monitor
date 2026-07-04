using System.ComponentModel;
using System.Diagnostics;

namespace NativeGuard_App.Processes;

internal sealed record ProcessTerminationResult(bool Succeeded, string Message);

internal sealed class ProcessTerminator
{
    public ProcessTerminationResult Terminate(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            process.Kill();
            return new ProcessTerminationResult(true, "已请求结束");
        }
        catch (ArgumentException)
        {
            return new ProcessTerminationResult(false, "进程已退出");
        }
        catch (InvalidOperationException)
        {
            return new ProcessTerminationResult(false, "进程已退出");
        }
        catch (Win32Exception ex)
        {
            return new ProcessTerminationResult(false, $"无法结束：{ex.Message}");
        }
        catch (NotSupportedException)
        {
            return new ProcessTerminationResult(false, "不支持结束该进程");
        }
    }
}
