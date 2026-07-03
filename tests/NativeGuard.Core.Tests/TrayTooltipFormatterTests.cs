using NativeGuard.Core.Processes;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class TrayTooltipFormatterTests
{
    [TestMethod]
    public void FormatTopProcesses_ReturnsEmptyMessage_WhenNoProcessesExist()
    {
        string result = TrayTooltipFormatter.FormatTopProcesses([], 5);

        Assert.AreEqual("Native Guard: no non-native processes", result);
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

        StringAssert.StartsWith(result, "Native Guard: Top 2 non-native by CPU time");
        Assert.IsLessThan(
            result.IndexOf("middle", StringComparison.Ordinal),
            result.IndexOf("fast", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("slow", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FormatTopProcesses_IncludesNamePidArchitectureAndFormattedCpuTime()
    {
        NonNativeProcessInfo[] processes =
        [
            new("chrome", 1234, "x64", TimeSpan.FromSeconds(61))
        ];

        string result = TrayTooltipFormatter.FormatTopProcesses(processes, 5);

        StringAssert.Contains(result, "chrome");
        StringAssert.Contains(result, "PID 1234");
        StringAssert.Contains(result, "x64");
        StringAssert.Contains(result, "1m 01s");
    }
}
