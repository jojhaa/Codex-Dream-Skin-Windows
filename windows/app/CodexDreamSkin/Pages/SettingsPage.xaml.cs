using CodexDreamSkin.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;

namespace CodexDreamSkin.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ResourceLoader _resources = ResourceLoader.GetForViewIndependentUse();
    private bool _initializing;

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
        }
        finally
        {
            _initializing = false;
        }
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
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
}
