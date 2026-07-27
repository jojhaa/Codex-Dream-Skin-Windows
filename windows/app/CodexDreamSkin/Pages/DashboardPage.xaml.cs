using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CodexDreamSkin.Models;
using CodexDreamSkin.Services;

namespace CodexDreamSkin.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly CodexThemeEngine _engine;

    public DashboardPage()
    {
        InitializeComponent();
        _engine = ((App)Application.Current).ThemeEngine;
        _engine.SnapshotChanged += Engine_SnapshotChanged;
        Unloaded += DashboardPage_Unloaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        RootScrollViewer.ChangeView(null, 0, null, true);
        EnsureFreeSoftwareNotice();
        await RunEngineOperationAsync(() => _engine.InspectAsync());
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e) => _engine.SnapshotChanged -= Engine_SnapshotChanged;

    private async void ApplyThemeButton_Click(object sender, RoutedEventArgs e) =>
        await RunEngineOperationAsync(() => _engine.StartOrApplyAsync());

    private async void RefreshStatusButton_Click(object sender, RoutedEventArgs e) =>
        await RunEngineOperationAsync(() => _engine.InspectAsync());

    private async Task RunEngineOperationAsync(Func<Task<EngineSnapshot>> operation)
    {
        SetBusy(true);
        try { RenderSnapshot(await operation()); }
        catch (OperationCanceledException) { }
        finally { SetBusy(false); }
    }

    private void Engine_SnapshotChanged(object? sender, EngineSnapshot snapshot) =>
        DispatcherQueue.TryEnqueue(() => RenderSnapshot(snapshot));

    private void RenderSnapshot(EngineSnapshot snapshot)
    {
        EngineStatusTitleText.Text = snapshot.Summary;
        EngineStatusBodyText.Text = snapshot.PackageVersion is null
            ? snapshot.Detail
            : $"{snapshot.Detail}\nCodex {snapshot.PackageVersion}";
        EngineStatusIcon.Symbol = snapshot.State switch
        {
            EngineState.Active => Symbol.Accept,
            EngineState.Faulted or EngineState.CodexNotInstalled => Symbol.Important,
            EngineState.Connecting => Symbol.Sync,
            _ => Symbol.Clock
        };
        ApplyThemeButton.IsEnabled = snapshot.State is not EngineState.Connecting and not EngineState.CodexNotInstalled;
    }

    private void SetBusy(bool busy)
    {
        EngineProgressRing.IsActive = busy;
        EngineProgressRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        RefreshStatusButton.IsEnabled = !busy;
        if (busy) ApplyThemeButton.IsEnabled = false;
    }

    private void EnsureFreeSoftwareNotice()
    {
        if (!FreeSoftwareNotice.IsCanonical(FreeSoftwareNoticeBodyText.Text))
        {
            FreeSoftwareNoticeBodyText.Text = FreeSoftwareNotice.ForCurrentLanguage();
        }

        OfficialProjectLink.NavigateUri = new Uri(FreeSoftwareNotice.ProjectUrl);
    }

    private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var isWide = e.NewSize.Width >= 920;

        DashboardPrimaryGrid.ColumnDefinitions[0].Width = new GridLength(1.08, GridUnitType.Star);
        DashboardPrimaryGrid.ColumnDefinitions[1].Width = isWide
            ? new GridLength(0.92, GridUnitType.Star)
            : new GridLength(0);
        DashboardArtwork.Visibility = isWide ? Visibility.Visible : Visibility.Collapsed;

        Grid.SetRow(AppStatusCard, 0);
        Grid.SetColumn(AppStatusCard, 0);
        Grid.SetColumnSpan(AppStatusCard, isWide ? 1 : 2);

        Grid.SetRow(ThemeStatusCard, isWide ? 0 : 1);
        Grid.SetColumn(ThemeStatusCard, isWide ? 1 : 0);
        Grid.SetColumnSpan(ThemeStatusCard, isWide ? 1 : 2);

        Grid.SetRow(DashboardInfo, isWide ? 0 : 1);
        Grid.SetColumn(DashboardInfo, isWide ? 1 : 0);
        Grid.SetColumnSpan(DashboardInfo, isWide ? 1 : 2);
    }
}
