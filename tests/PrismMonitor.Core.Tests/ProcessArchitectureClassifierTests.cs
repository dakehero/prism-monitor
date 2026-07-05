using PrismMonitor.Core.Processes;
using System.Reflection.PortableExecutable;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class ProcessArchitectureClassifierTests
{
    [TestMethod]
    public void Classify_ReturnsNative_ForArm64ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Arm64,
            (ushort)Machine.Arm64);

        Assert.IsFalse(result.IsCompatibility);
        Assert.AreEqual("ARM64", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsCompatibilityX64_ForAmd64ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Amd64,
            (ushort)Machine.Arm64);

        Assert.IsTrue(result.IsCompatibility);
        Assert.AreEqual("x64", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsCompatibilityX86_ForI386ProcessOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.I386,
            (ushort)Machine.Arm64);

        Assert.IsTrue(result.IsCompatibility);
        Assert.AreEqual("x86", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsCompatibilityArm64Ec_WhenArm64EcValueIsReportedOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            ProcessArchitectureClassifier.Arm64EcMachine,
            (ushort)Machine.Arm64);

        Assert.IsTrue(result.IsCompatibility);
        Assert.AreEqual("ARM64EC", result.DisplayName);
    }

    [TestMethod]
    public void Classify_ReturnsCompatibilityArm64X_WhenArm64XValueIsReportedOnArm64Host()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            ProcessArchitectureClassifier.Arm64XMachine,
            (ushort)Machine.Arm64);

        Assert.IsTrue(result.IsCompatibility);
        Assert.AreEqual("ARM64X", result.DisplayName);
    }

    [TestMethod]
    public void Classify_DoesNotTreatUnknownProcessMachineAsCompatibility()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Unknown,
            (ushort)Machine.Arm64);

        Assert.IsFalse(result.IsCompatibility);
        Assert.AreEqual("Unknown", result.DisplayName);
    }

    [TestMethod]
    public void Classify_UsesImageMachine_WhenProcessMachineIsUnknown()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Unknown,
            (ushort)Machine.Arm64,
            (ushort)Machine.Amd64);

        Assert.IsTrue(result.IsCompatibility);
        Assert.AreEqual("x64", result.DisplayName);
    }

    [TestMethod]
    public void Classify_UsesArm64XImageMachine_WhenProcessMachineIsUnknown()
    {
        ProcessArchitectureInfo result = ProcessArchitectureClassifier.Classify(
            (ushort)Machine.Unknown,
            (ushort)Machine.Arm64,
            ProcessArchitectureClassifier.Arm64XMachine);

        Assert.IsTrue(result.IsCompatibility);
        Assert.AreEqual("ARM64X", result.DisplayName);
    }
}
