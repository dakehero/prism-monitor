using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class PeImageArchitectureDetectorTests
{
    [TestMethod]
    public void HasArm64XMetadata_ReturnsTrue_WhenSectionTableContainsArm64XMetadataSection()
    {
        IReadOnlyList<string> sectionNames = [".text", ".rdata", ".a64xrm", ".reloc"];

        Assert.IsTrue(PeImageArchitectureDetector.HasArm64XMetadata(sectionNames));
    }

    [TestMethod]
    public void HasArm64XMetadata_ReturnsTrue_WhenSectionTableContainsHybridThunkSection()
    {
        IReadOnlyList<string> sectionNames = [".text", ".hexpthk", ".rdata"];

        Assert.IsTrue(PeImageArchitectureDetector.HasArm64XMetadata(sectionNames));
    }

    [TestMethod]
    public void HasArm64XMetadata_ReturnsFalse_ForPlainX64Sections()
    {
        IReadOnlyList<string> sectionNames = [".text", ".rdata", ".pdata", ".reloc"];

        Assert.IsFalse(PeImageArchitectureDetector.HasArm64XMetadata(sectionNames));
    }
}
