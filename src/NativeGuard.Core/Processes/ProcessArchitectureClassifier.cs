namespace NativeGuard.Core.Processes;

public sealed record ProcessArchitectureInfo(bool IsNonNative, string DisplayName);

public static class ProcessArchitectureClassifier
{
    public static ProcessArchitectureInfo Classify(MachineType processMachine, MachineType nativeMachine)
    {
        string displayName = GetDisplayName(processMachine);
        bool isNonNative = nativeMachine == MachineType.Arm64
            && (processMachine == MachineType.Amd64 || processMachine == MachineType.I386);

        return new ProcessArchitectureInfo(isNonNative, displayName);
    }

    private static string GetDisplayName(MachineType machineType)
    {
        return machineType switch
        {
            MachineType.I386 => "x86",
            MachineType.Amd64 => "x64",
            MachineType.Arm64 => "ARM64",
            _ => "Unknown"
        };
    }
}
