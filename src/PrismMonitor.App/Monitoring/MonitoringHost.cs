using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PrismMonitor.App.Diagnostics;
using PrismMonitor.App.Power;
using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Power;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Settings;

namespace PrismMonitor.App.Monitoring;

internal sealed class MonitoringHost : IAsyncDisposable
{
    private static readonly TimeSpan InteractionDebounce = TimeSpan.FromSeconds(2);
    private readonly MonitoringCoordinator _coordinator;
    private readonly IgnoredProcessStore _ignoredProcessStore;
    private readonly MonitoringSettingsStore _settingsStore;
    private readonly PowerStatusProvider _powerStatusProvider;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherTimer _timer = new DispatcherTimer();
    private readonly object _interactionSync = new();
    private DateTimeOffset _lastInteractionRefresh = DateTimeOffset.MinValue;
    private bool _isMainWindowVisible;
    private bool _isStarted;
    private bool _isDisposed;

    public MonitoringHost(
        MonitoringCoordinator coordinator,
        IgnoredProcessStore ignoredProcessStore,
        MonitoringSettingsStore settingsStore,
        PowerStatusProvider powerStatusProvider,
        DispatcherQueue dispatcherQueue)
    {
        _coordinator = coordinator;
        _ignoredProcessStore = ignoredProcessStore;
        _settingsStore = settingsStore;
        _powerStatusProvider = powerStatusProvider;
        _dispatcherQueue = dispatcherQueue;
        _timer.Tick += Timer_Tick;
        _coordinator.SnapshotPublished += Coordinator_SnapshotPublished;
    }

    public event EventHandler<MonitoringSnapshot>? SnapshotPublished;

    public MonitoringSnapshot? LatestSnapshot => _coordinator.LatestSnapshot;

    public MonitoringCycleDiagnostics LastDiagnostics => _coordinator.LastDiagnostics;

    public TimeSpan CurrentInterval => _timer.Interval;

    public async Task StartAsync()
    {
        if (_isStarted || _isDisposed)
        {
            return;
        }

        await LoadConfigurationAsync();
        _powerStatusProvider.PowerSourceChanged += PowerStatusProvider_PowerSourceChanged;
        _isStarted = true;
        RestartTimer();
        await RequestRefreshAsync(MonitoringRefreshReason.Interaction);
    }

    public void SetMainWindowVisible(bool isVisible)
    {
        if (_isDisposed)
        {
            return;
        }

        _isMainWindowVisible = isVisible;
        if (_isStarted)
        {
            RestartTimer();
            _ = RequestRefreshAsync(
                isVisible ? MonitoringRefreshReason.WindowVisible : MonitoringRefreshReason.Interaction,
                fullDetails: isVisible);
        }
    }

    public Task RequestRefreshAsync(
        MonitoringRefreshReason reason,
        bool fullDetails = false)
    {
        if (_isDisposed || ShouldDebounce(reason))
        {
            return Task.CompletedTask;
        }

        return RequestRefreshCoreAsync(reason, fullDetails);
    }

    public async Task ReloadConfigurationAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await LoadConfigurationAsync();
        await RequestRefreshAsync(MonitoringRefreshReason.ConfigurationChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _powerStatusProvider.PowerSourceChanged -= PowerStatusProvider_PowerSourceChanged;
        _coordinator.SnapshotPublished -= Coordinator_SnapshotPublished;
        try
        {
            await _coordinator.StopAsync();
        }
        finally
        {
            _powerStatusProvider.Dispose();
        }
    }

    private async Task LoadConfigurationAsync()
    {
        IReadOnlyList<PrismMonitor.Core.Rules.AppIdentityRule> rules =
            await _ignoredProcessStore.GetRulesAsync();
        MonitoringSettings settings = await _settingsStore.GetAsync();
        _coordinator.UpdateConfiguration(new MonitoringConfiguration(rules, settings));
    }

    private async Task RequestRefreshCoreAsync(
        MonitoringRefreshReason reason,
        bool fullDetails)
    {
        try
        {
            await _coordinator.RequestRefreshAsync(new MonitoringRefreshRequest(reason, fullDetails));
        }
        catch (OperationCanceledException) when (_isDisposed)
        {
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("MonitoringHost.RequestRefresh", exception);
        }
    }

    private bool ShouldDebounce(MonitoringRefreshReason reason)
    {
        if (reason != MonitoringRefreshReason.Interaction)
        {
            return false;
        }

        lock (_interactionSync)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastInteractionRefresh < InteractionDebounce)
            {
                return true;
            }

            _lastInteractionRefresh = now;
            return false;
        }
    }

    private void RestartTimer()
    {
        _timer.Stop();
        _timer.Interval = RefreshSchedulePolicy.GetRefreshInterval(
            _powerStatusProvider.GetCurrentPowerSource(),
            _isMainWindowVisible);
        _timer.Start();
    }

    private async void Timer_Tick(object? sender, object e)
    {
        await RequestRefreshAsync(MonitoringRefreshReason.Periodic);
    }

    private void PowerStatusProvider_PowerSourceChanged(object? sender, EventArgs e)
    {
        _ = _dispatcherQueue.TryEnqueue(() =>
        {
            RestartTimer();
            _ = RequestRefreshAsync(MonitoringRefreshReason.PowerChanged);
        });
    }

    private void Coordinator_SnapshotPublished(object? sender, MonitoringSnapshot snapshot)
    {
        SnapshotPublished?.Invoke(this, snapshot);
    }
}
