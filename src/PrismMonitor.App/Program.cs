using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PrismMonitor.App.Diagnostics;
using WinRT;

namespace PrismMonitor.App;

public static class Program
{
    private const string MainInstanceKey = "main";

    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiagnostics.Register();

        try
        {
            ComWrappersSupport.InitializeComWrappers();

            if (RedirectActivationToMainInstance())
            {
                return;
            }

            Application.Start(_ =>
            {
                DispatcherQueueSynchronizationContext context = new(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                App app = new();
                GC.KeepAlive(app);
            });
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("Program.Main", ex);
            throw;
        }
    }

    private static bool RedirectActivationToMainInstance()
    {
        AppInstance mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        if (mainInstance.IsCurrent)
        {
            return false;
        }

        AppActivationArguments activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        mainInstance.RedirectActivationToAsync(activationArguments).AsTask().GetAwaiter().GetResult();
        return true;
    }
}
