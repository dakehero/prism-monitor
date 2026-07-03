using NativeGuard.Core.Processes;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class ProcessArchitectureClassifierTests
{
    [TestMethod]
    public void Classify_ReturnsNative_ForArm64ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            MachineType.Arm64,
            MachineType.Arm64);

        Assert.IsFalse(result.IsNonNative);
        Assert.AreEqual("ARM64", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsNonNativeX64_ForAmd64ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            MachineType.Amd64,
            MachineType.Arm64);

        Assert.IsTrue(result.IsNonNative);
        Assert.AreEqual("x64", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsNonNativeX86_ForI386ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            MachineType.I386,
            MachineType.Arm64);

        Assert.IsTrue(result.IsNonNative);
        Assert.AreEqual("x86", result.DisplayName);
    }

    [TestMethod]
    public void Classify_DoesNotTreatUnknownProcessMachineAsNonNative()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            MachineType.Unknown,
            MachineType.Arm64);

        Assert.IsFalse(result.IsNonNative);
        Assert.AreEqual("Unknown", result.DisplayName);
    }
}
