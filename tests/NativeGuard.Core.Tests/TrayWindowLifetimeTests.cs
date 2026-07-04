using NativeGuard.Core.Ui;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class TrayWindowLifetimeTests
{
    [TestMethod]
    public void NeedsWindow_ReturnsFalse_AfterWindowIsCreatedAndHiddenToTray()
    {
        TrayWindowLifetime lifetime = new();

        Assert.IsTrue(lifetime.NeedsWindow);

        lifetime.MarkWindowCreated();
        lifetime.MarkHiddenToTray();

        Assert.IsFalse(lifetime.NeedsWindow);
    }

    [TestMethod]
    public void NeedsWindow_ReturnsTrue_AfterExitCloseCompletes()
    {
        TrayWindowLifetime lifetime = new();
        lifetime.MarkWindowCreated();

        lifetime.RequestExitClose();
        lifetime.MarkWindowClosed();

        Assert.IsTrue(lifetime.NeedsWindow);
    }
}
