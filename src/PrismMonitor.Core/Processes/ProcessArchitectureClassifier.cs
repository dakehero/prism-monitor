using System.Reflection.PortableExecutable;

namespace PrismMonitor.Core.Processes;

public sealed record ProcessArchitectureInfo(bool IsCompatibility, string DisplayName);

public static class ProcessArchitectureClassifier
{
    public const ushort Arm64EcMachine = 0xa641;
    public const ushort Arm64XMachine = 0xa64e;

    public static ProcessArchitectureInfo Classify(ushort processMachine, ushort nativeMachine)
    {
        return Classify(processMachine, nativeMachine, imageMachine: null);
    }

    public static ProcessArchitectureInfo Classify(ushort processMachine, ushort nativeMachine, ushort? imageMachine)
    {
        return Classify(processMachine, nativeMachine, imageMachine, imageHasArm64XMetadata: false);
    }

    public static ProcessArchitectureInfo Classify(
        ushort processMachine,
        ushort nativeMachine,
        ushort? imageMachine,
        bool imageHasArm64XMetadata)
    {
        string displayName = GetDisplayName(processMachine);
        ushort effectiveMachine = processMachine == (ushort)Machine.Unknown && imageMachine.HasValue
            ? imageMachine.Value
            : processMachine;

        if (processMachine == (ushort)Machine.Unknown
            && imageMachine == (ushort)Machine.Amd64
            && imageHasArm64XMetadata)
        {
            return new ProcessArchitectureInfo(
                IsCompatibility: nativeMachine == (ushort)Machine.Arm64,
                DisplayName: "ARM64EC");
        }

        if (processMachine == (ushort)Machine.Unknown && imageMachine.HasValue)
        {
            displayName = GetDisplayName(imageMachine.Value);
        }

        bool isArm64Host = nativeMachine == (ushort)Machine.Arm64;
        bool isCompatibility = isArm64Host
            && (effectiveMachine == (ushort)Machine.Amd64
                || effectiveMachine == (ushort)Machine.I386
                || effectiveMachine == Arm64EcMachine
                || effectiveMachine == Arm64XMachine);

        return new ProcessArchitectureInfo(isCompatibility, displayName);
    }

    private static string GetDisplayName(ushort machineType)
    {
        return machineType switch
        {
            (ushort)Machine.I386 => "x86",
            (ushort)Machine.Amd64 => "x64",
            (ushort)Machine.Arm64 => "ARM64",
            Arm64EcMachine => "ARM64EC",
            Arm64XMachine => "ARM64X",
            _ => "Unknown"
        };
    }
}
