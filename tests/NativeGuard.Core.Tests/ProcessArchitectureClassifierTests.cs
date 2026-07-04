using NativeGuard.Core.Processes;
using System.Reflection.PortableExecutable;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class ProcessArchitectureClassifierTests
{
    [TestMethod]
    public void Classify_ReturnsNative_ForArm64ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Arm64,
            (ushort)Machine.Arm64);

        Assert.IsFalse(result.IsNonNative);
        Assert.AreEqual("ARM64", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsNonNativeX64_ForAmd64ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Amd64,
            (ushort)Machine.Arm64);

        Assert.IsTrue(result.IsNonNative);
        Assert.AreEqual("x64", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsNonNativeX86_ForI386ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.I386,
            (ushort)Machine.Arm64);

        Assert.IsTrue(result.IsNonNative);
        Assert.AreEqual("x86", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsNonNativeArm64Ec_WhenArm64EcValueIsReportedOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            ProcessArchitectureClassifier.Arm64EcMachine,
            (ushort)Machine.Arm64);

        Assert.IsTrue(result.IsNonNative);
        Assert.AreEqual("ARM64EC", result.DisplayName);
    }

    [TestMethod]
    public void Classify_DoesNotTreatUnknownProcessMachineAsNonNative()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Unknown,
            (ushort)Machine.Arm64);

        Assert.IsFalse(result.IsNonNative);
        Assert.AreEqual("Unknown", result.DisplayName);
    }

    [TestMethod]
    public void Classify_UsesImageMachine_WhenProcessMachineIsUnknown()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Unknown,
            (ushort)Machine.Arm64,
            (ushort)Machine.Amd64);

        Assert.IsTrue(result.IsNonNative);
        Assert.AreEqual("x64", result.DisplayName);
    }
}
