using System.Runtime.InteropServices;
using PrismMonitor.Core.Power;

namespace PrismMonitor.App.Power;

internal sealed partial class PowerStatusProvider : IDisposable
{
    public event EventHandler? PowerSourceChanged;

    public PowerStatusProvider()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    public PowerSource GetCurrentPowerSource()
    {
        if (!GetSystemPowerStatus(out SystemPowerStatus status))
        {
            return PowerSource.Unknown;
        }

        return status.ACLineStatus switch
        {
            0 => PowerSource.Battery,
            1 => PowerSource.AC,
            _ => PowerSource.Unknown
        };
    }

    public void Dispose()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    }

    private void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        PowerSourceChanged?.Invoke(this, EventArgs.Empty);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
