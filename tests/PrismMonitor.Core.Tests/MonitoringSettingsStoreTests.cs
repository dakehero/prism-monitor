using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class MonitoringSettingsStoreTests
{
    private string _temporaryDirectory = string.Empty;

    [TestInitialize]
    public void TestInitialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetAsync_ReturnsDefaults_WhenFileDoesNotExist()
    {
        MonitoringSettings settings = await CreateStore().GetAsync();

        Assert.IsTrue(settings.IncludeArm64EcProcesses);
        Assert.AreEqual(NotificationLevel.X86X64AndArm64Ec, settings.NotificationLevel);
    }

    [TestMethod]
    public async Task SaveAsync_PersistsSettings()
    {
        MonitoringSettingsStore store = CreateStore();
        MonitoringSettings expected = new(IncludeArm64EcProcesses: false, NotificationLevel: NotificationLevel.X86AndX64);

        await store.SaveAsync(expected);
        MonitoringSettings actual = await store.GetAsync();

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task GetAsync_ReturnsDefaults_WhenJsonIsInvalid()
    {
        string filePath = Path.Combine(_temporaryDirectory, "settings.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json");

        MonitoringSettings settings = await new MonitoringSettingsStore(filePath).GetAsync();

        Assert.AreEqual(MonitoringSettings.Default, settings);
    }

    private MonitoringSettingsStore CreateStore()
    {
        return new MonitoringSettingsStore(Path.Combine(_temporaryDirectory, "settings.json"));
    }
}
