using PrismMonitor.Core.Notifications;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class NotificationActivationParserTests
{
    [TestMethod]
    public void Parse_ReturnsOpenProcess_ForToastBodyActivation()
    {
        NotificationActivation activation = NotificationActivationParser.Parse(new Dictionary<string, string>
        {
            ["action"] = "open",
            ["pid"] = "42",
            ["name"] = "Chrome"
        });

        Assert.AreEqual(NotificationActivationKind.OpenProcess, activation.Kind);
        Assert.AreEqual(42, activation.ProcessId);
        Assert.AreEqual("Chrome", activation.ProcessName);
    }

    [TestMethod]
    public void Parse_ReturnsTerminateProcess_ForTerminateAction()
    {
        NotificationActivation activation = NotificationActivationParser.Parse(new Dictionary<string, string>
        {
            ["action"] = "terminate",
            ["pid"] = "42"
        });

        Assert.AreEqual(NotificationActivationKind.TerminateProcess, activation.Kind);
        Assert.AreEqual(42, activation.ProcessId);
    }

    [TestMethod]
    public void Parse_ReturnsIgnoreProcess_ForIgnoreAction()
    {
        NotificationActivation activation = NotificationActivationParser.Parse(new Dictionary<string, string>
        {
            ["action"] = "ignore",
            ["name"] = "Chrome"
        });

        Assert.AreEqual(NotificationActivationKind.IgnoreProcess, activation.Kind);
        Assert.AreEqual("Chrome", activation.ProcessName);
    }

    [TestMethod]
    public void Parse_ReturnsNone_ForInvalidOpenArguments()
    {
        NotificationActivation activation = NotificationActivationParser.Parse(new Dictionary<string, string>
        {
            ["action"] = "open",
            ["pid"] = "not-a-pid",
            ["name"] = "Chrome"
        });

        Assert.AreEqual(NotificationActivationKind.None, activation.Kind);
    }

    [TestMethod]
    public void Parse_ReturnsNone_ForOpenWithoutProcessName()
    {
        NotificationActivation activation = NotificationActivationParser.Parse(new Dictionary<string, string>
        {
            ["action"] = "open",
            ["pid"] = "42"
        });

        Assert.AreEqual(NotificationActivationKind.None, activation.Kind);
    }

    [TestMethod]
    public void Parse_ReturnsNone_ForOpenWithBlankProcessName()
    {
        NotificationActivation activation = NotificationActivationParser.Parse(new Dictionary<string, string>
        {
            ["action"] = "open",
            ["pid"] = "42",
            ["name"] = " "
        });

        Assert.AreEqual(NotificationActivationKind.None, activation.Kind);
    }

    [TestMethod]
    public void Parse_ReturnsNone_ForUnknownAction()
    {
        NotificationActivation activation = NotificationActivationParser.Parse(new Dictionary<string, string>
        {
            ["action"] = "something-else",
            ["pid"] = "42",
            ["name"] = "Chrome"
        });

        Assert.AreEqual(NotificationActivationKind.None, activation.Kind);
    }
}
