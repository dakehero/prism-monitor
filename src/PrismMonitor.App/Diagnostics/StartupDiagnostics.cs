namespace PrismMonitor.App.Diagnostics;

internal static class StartupDiagnostics
{
    private static readonly object Gate = new();

    public static void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Write("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write("TaskScheduler.UnobservedTaskException", args.Exception);
        };
    }

    public static void Write(string source, Exception? exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrismMonitor");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, "startup-crash.log");
            string message = string.Concat(
                DateTimeOffset.Now.ToString("O"),
                " ",
                source,
                Environment.NewLine,
                exception?.ToString() ?? "No exception object was provided.",
                Environment.NewLine,
                Environment.NewLine);

            lock (Gate)
            {
                File.AppendAllText(path, message);
            }
        }
        catch
        {
            // Last-resort diagnostics must never create a second startup failure.
        }
    }
}
