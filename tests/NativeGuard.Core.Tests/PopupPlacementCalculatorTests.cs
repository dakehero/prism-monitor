using NativeGuard.Core.Ui;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class PopupPlacementCalculatorTests
{
    [TestMethod]
    public void Calculate_PlacesWindowAboveTrayIcon_WhenThereIsRoom()
    {
        ScreenPoint point = PopupPlacementCalculator.Calculate(
            new ScreenRect(1840, 1030, 32, 32),
            new ScreenSize(640, 420),
            new ScreenRect(0, 0, 1920, 1080));

        Assert.AreEqual(1280, point.X);
        Assert.AreEqual(610, point.Y);
    }

    [TestMethod]
    public void Calculate_ClampsWindowInsideWorkArea()
    {
        ScreenPoint point = PopupPlacementCalculator.Calculate(
            new ScreenRect(10, 1030, 32, 32),
            new ScreenSize(640, 420),
            new ScreenRect(0, 0, 1920, 1080));

        Assert.AreEqual(0, point.X);
        Assert.AreEqual(610, point.Y);
    }

    [TestMethod]
    public void Calculate_PlacesWindowBelowIcon_WhenAboveWouldLeaveWorkArea()
    {
        ScreenPoint point = PopupPlacementCalculator.Calculate(
            new ScreenRect(900, 10, 32, 32),
            new ScreenSize(640, 420),
            new ScreenRect(0, 0, 1920, 1080));

        Assert.AreEqual(596, point.X);
        Assert.AreEqual(42, point.Y);
    }
}
