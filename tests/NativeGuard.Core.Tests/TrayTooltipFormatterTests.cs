using NativeGuard.Core.Processes;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class TrayTooltipFormatterTests
{
    [TestMethod]
    public void FormatTopProcesses_ReturnsEmptyMessage_WhenNoProcessesExist()
    {
        string result = TrayTooltipFormatter.FormatTopProcesses([], 5);

        Assert.AreEqual("没有非原生进程", result);
    }

    [TestMethod]
    public void FormatTopProcesses_SortsByCpuTimeDescending_AndLimitsToTopN()
    {
        NonNativeProcessInfo[] processes =
        [
            new("slow", 10, "x64", TimeSpan.FromSeconds(5)),
            new("fast", 11, "x86", TimeSpan.FromSeconds(65)),
            new("middle", 12, "x64", TimeSpan.FromSeconds(30))
        ];

        string result = TrayTooltipFormatter.FormatTopProcesses(processes, 2);

        Assert.IsLessThan(
            result.IndexOf("middle", StringComparison.Ordinal),
            result.IndexOf("fast", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("slow", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("Native Guard", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("Top", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FormatTopProcesses_IncludesOnlyProcessNames()
    {
        NonNativeProcessInfo[] processes =
        [
            new("chrome", 1234, "x64", TimeSpan.FromSeconds(61))
        ];

        string result = TrayTooltipFormatter.FormatTopProcesses(processes, 5);

        StringAssert.Contains(result, "chrome");
        Assert.IsFalse(result.Contains("#1234", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("x64", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("1m 01s", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FormatTopProcesses_UsesOneProcessNamePerRow()
    {
        NonNativeProcessInfo[] processes =
        [
            new("visualstudio", 1234, "x64", TimeSpan.FromSeconds(3661)),
            new("photoshop", 5678, "x64", TimeSpan.FromSeconds(62))
        ];

        string result = TrayTooltipFormatter.FormatTopProcesses(processes, 2);

        string[] lines = result.Split(Environment.NewLine);
        Assert.HasCount(2, lines);
        Assert.AreEqual("visualstudio", lines[0]);
        Assert.AreEqual("photoshop", lines[1]);
    }
}
