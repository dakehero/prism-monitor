namespace PrismMonitor.Core.Rules;

[Flags]
public enum SuppressionTarget
{
    None = 0,
    Processes = 1,
    History = 2,
    Tray = 4,
    Toast = 8,
    All = Processes | History | Tray | Toast
}
