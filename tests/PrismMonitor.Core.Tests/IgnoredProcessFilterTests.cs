using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class IgnoredProcessFilterTests
{
    [TestMethod]
    public void Filter_RemovesProcessesByName_CaseInsensitive()
    {
        CompatibilityProcessInfo[] processes =
        [
            new("Chrome", 10, "x64", TimeSpan.FromSeconds(1)),
            new("legacy", 20, "x86", TimeSpan.FromSeconds(2))
        ];

        IReadOnlyList<CompatibilityProcessInfo> result = IgnoredProcessFilter.Filter(processes, ["chrome"]);

        CollectionAssert.AreEqual(new[] { "legacy" }, result.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    public void Filter_TreatsExeSuffixAsOptional()
    {
        CompatibilityProcessInfo[] processes =
        [
            new("foo", 10, "x64", TimeSpan.FromSeconds(1)),
            new("bar.exe", 20, "x86", TimeSpan.FromSeconds(2))
        ];

        IReadOnlyList<CompatibilityProcessInfo> result = IgnoredProcessFilter.Filter(processes, ["FOO.EXE", "bar"]);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void NormalizeName_ReturnsEmptyString_ForWhitespace()
    {
        string result = IgnoredProcessFilter.NormalizeName("   ");

        Assert.AreEqual(string.Empty, result);
    }
}
