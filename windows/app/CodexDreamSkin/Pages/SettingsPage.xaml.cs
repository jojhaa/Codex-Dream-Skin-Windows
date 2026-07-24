using CodexDreamSkin.Models;
using CodexDreamSkin.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel;

namespace CodexDreamSkin.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ResourceLoader _resources = new();
    private bool _initializing;
    private CancellationTokenSource? _versionCheckCancellation;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.Current is not App app)
        {
            return;
        }

        _initializing = true;
        try
        {
            AppearanceSelector.SelectedIndex = app.ManagerSettings.Appearance switch
            {
                "light" => 1,
                "dark" => 2,
                _ => 0,
            };
            AutoTakeoverToggle.IsOn = app.ManagerSettings.AutoTakeoverEnabled;
            ApplyTakeoverSnapshot(app.TakeoverService.Snapshot);
            app.TakeoverService.SnapshotChanged += TakeoverService_SnapshotChanged;

            var startupState = await app.StartupTasks.GetStateAsync();
            StartupTaskToggle.IsOn = startupState == StartupTaskState.Enabled;
            UpdateStartupStatus(startupState);

            CurrentVersionText.Text = $"v{ReleaseCheckService.CurrentVersionLabel}";
            EnsureFreeSoftwareNotice();
        }
        finally
        {
            _initializing = false;
        }
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _versionCheckCancellation?.Cancel();
        _versionCheckCancellation?.Dispose();
        _versionCheckCancellation = null;

        if (App.Current is App app)
        {
            app.TakeoverService.SnapshotChanged -= TakeoverService_SnapshotChanged;
        }
    }

    private void AppearanceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppearanceSelector.SelectedItem is not ComboBoxItem item ||
            App.Current is not App app ||
            app.MainWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        var appearance = item.Tag?.ToString() ?? "system";
        app.ManagerSettings.Appearance = appearance;
        root.RequestedTheme = appearance switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private async void AutoTakeoverToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing || App.Current is not App app)
        {
            return;
        }

        if (AutoTakeoverToggle.IsOn)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = _resources.GetString("AutoTakeoverDialogTitle"),
                Content = _resources.GetString("AutoTakeoverDialogBody"),
                PrimaryButtonText = _resources.GetString("AutoTakeoverDialogConfirm"),
                CloseButtonText = _resources.GetString("AutoTakeoverDialogCancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                _initializing = true;
                AutoTakeoverToggle.IsOn = false;
                _initializing = false;
                return;
            }

            app.ManagerSettings.AutoTakeoverEnabled = true;
            app.TakeoverService.Start();

            var startupState = await app.StartupTasks.SetEnabledAsync(true);
            _initializing = true;
            StartupTaskToggle.IsOn = startupState == StartupTaskState.Enabled;
            _initializing = false;
            UpdateStartupStatus(startupState);
        }
        else
        {
            app.ManagerSettings.AutoTakeoverEnabled = false;
            await app.TakeoverService.StopAsync();
        }
    }

    private async void StartupTaskToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing || App.Current is not App app)
        {
            return;
        }

        var state = await app.StartupTasks.SetEnabledAsync(StartupTaskToggle.IsOn);
        _initializing = true;
        StartupTaskToggle.IsOn = state == StartupTaskState.Enabled;
        _initializing = false;
        UpdateStartupStatus(state);
    }

    private async void ExitManagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current is App app)
        {
            await app.ExitManagerAsync();
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current is not App app)
        {
            return;
        }

        _versionCheckCancellation?.Cancel();
        _versionCheckCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _versionCheckCancellation = cancellation;
        SetVersionCheckBusy(true);
        VersionStatusText.Text = _resources.GetString("VersionCheckRunning");

        try
        {
            var result = await app.ReleaseChecks.CheckLatestAsync(cancellation.Token);
            LatestReleaseLink.NavigateUri = result.ReleaseUri;
            VersionStatusText.Text = string.Format(
                _resources.GetString(
                    result.IsUpdateAvailable
                        ? "VersionUpdateAvailable"
                        : "VersionAlreadyCurrent"),
                result.LatestTag);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            VersionStatusText.Text = _resources.GetString("VersionCheckFailed");
        }
        finally
        {
            if (ReferenceEquals(_versionCheckCancellation, cancellation))
            {
                _versionCheckCancellation.Dispose();
                _versionCheckCancellation = null;
                SetVersionCheckBusy(false);
            }
        }
    }

    private void TakeoverService_SnapshotChanged(object? sender, TakeoverSnapshot snapshot)
    {
        DispatcherQueue.TryEnqueue(() => ApplyTakeoverSnapshot(snapshot));
    }

    private void ApplyTakeoverSnapshot(TakeoverSnapshot snapshot)
    {
        TakeoverStatusText.Text = $"{snapshot.Summary} · {snapshot.Detail}";
    }

    private void UpdateStartupStatus(StartupTaskState? state)
    {
        StartupTaskStatusText.Text = state switch
        {
            StartupTaskState.Enabled => _resources.GetString("StartupTaskEnabled"),
            StartupTaskState.DisabledByUser => _resources.GetString("StartupTaskDisabledByUser"),
            StartupTaskState.DisabledByPolicy => _resources.GetString("StartupTaskDisabledByPolicy"),
            StartupTaskState.Disabled => _resources.GetString("StartupTaskDisabled"),
            _ => _resources.GetString("StartupTaskUnavailable"),
        };
    }

    private void EnsureFreeSoftwareNotice()
    {
        if (!FreeSoftwareNotice.IsCanonical(FreeSoftwareNoticeBodyText.Text))
        {
            FreeSoftwareNoticeBodyText.Text = FreeSoftwareNotice.ForCurrentLanguage();
        }

        OfficialProjectLink.NavigateUri = new Uri(FreeSoftwareNotice.ProjectUrl);
        LatestReleaseLink.NavigateUri = new Uri(ReleaseCheckService.ReleasesPage);
    }

    private void SetVersionCheckBusy(bool busy)
    {
        VersionCheckProgressRing.IsActive = busy;
        VersionCheckProgressRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CheckUpdatesButton.IsEnabled = !busy;
    }
}
