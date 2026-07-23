using CodexDreamSkin.Models;
using CodexDreamSkin.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace CodexDreamSkin.Pages;

public sealed partial class DiagnosticsPage : Page
{
    private readonly CodexThemeEngine _engine;
    private readonly CodexPackageLocator _packageLocator = new();
    private readonly TcpListenerVerifier _listenerVerifier = new();
    private readonly ResourceLoader _resources = ResourceLoader.GetForViewIndependentUse();
    private CodexInstallation? _installation;

    public DiagnosticsPage()
    {
        InitializeComponent();
        _engine = ((App)Application.Current).ThemeEngine;
    }

    private async void DiagnosticsPage_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void RefreshPortsButton_Click(object sender, RoutedEventArgs e) => await RefreshPortsAsync();

    private async Task RefreshAsync()
    {
        var snapshot = await _engine.InspectAsync();
        PackageBodyText.Text = snapshot.PackageVersion is null
            ? "未找到已注册的 OpenAI.Codex 包。"
            : $"已动态解析当前 OpenAI.Codex {snapshot.PackageVersion}；应用更新后会自动重新发现。";
        PackageIcon.Symbol = snapshot.PackageVersion is null ? Symbol.Important : Symbol.Accept;
        RuntimeBodyText.Text = snapshot.ListenerProcessId is null
            ? snapshot.Detail
            : $"可信回环端口 127.0.0.1:{snapshot.ListenerPort ?? CodexThemeEngine.DefaultPort}，PID {snapshot.ListenerProcessId}，目标 {snapshot.TargetCount}。";
        RuntimeIcon.Symbol = snapshot.State switch
        {
            EngineState.Active => Symbol.Accept,
            EngineState.Faulted => Symbol.Important,
            _ => Symbol.Clock
        };
        await RefreshPortsAsync();
    }

    private Task RefreshPortsAsync()
    {
        _installation = _packageLocator.FindCurrent();
        if (_installation is null)
        {
            PortRowsControl.ItemsSource = null;
            PortsSummaryText.Text = Resource("PortPackageMissing");
            return Task.CompletedTask;
        }

        var inspections = _listenerVerifier.InspectManagedPorts(
            CodexThemeEngine.DefaultPort,
            CodexThemeEngine.LastManagedPort,
            _installation);
        PortRowsControl.ItemsSource = inspections.Select(CreatePortRow).ToArray();
        var occupied = inspections.Count(item => item.ProcessId is not null);
        PortsSummaryText.Text = string.Format(
            Resource("PortSummaryFormat"),
            CodexThemeEngine.DefaultPort,
            CodexThemeEngine.LastManagedPort,
            occupied);
        return Task.CompletedTask;
    }

    private async void ClosePortProcessButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PortRow row } button ||
            row.Inspection.ProcessId is not int processId ||
            _installation is null)
            return;

        var confirmation = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Resource("ClosePortDialogTitle"),
            Content = string.Format(Resource("ClosePortDialogBodyFormat"), processId, row.Inspection.Port),
            PrimaryButtonText = Resource("ClosePortDialogConfirm"),
            CloseButtonText = Resource("ClosePortDialogCancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;

        button.IsEnabled = false;
        PortActionInfoBar.IsOpen = false;
        try
        {
            var closed = await _listenerVerifier.TerminateManagedListenerAsync(
                row.Inspection.Port,
                processId,
                _installation);
            PortActionInfoBar.Severity = closed ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            PortActionInfoBar.Title = closed ? Resource("ClosePortSuccess") : Resource("ClosePortPending");
            PortActionInfoBar.Message = string.Format(Resource("ClosePortResultFormat"), row.Inspection.Port, processId);
        }
        catch (Exception error)
        {
            PortActionInfoBar.Severity = InfoBarSeverity.Error;
            PortActionInfoBar.Title = Resource("ClosePortFailed");
            PortActionInfoBar.Message = error.Message;
        }
        finally
        {
            PortActionInfoBar.IsOpen = true;
            await RefreshPortsAsync();
        }
    }

    private PortRow CreatePortRow(TcpListenerVerifier.ManagedPortInspection inspection)
    {
        var status = inspection.Kind switch
        {
            TcpListenerVerifier.ManagedPortKind.Free => Resource("PortStatusFree"),
            TcpListenerVerifier.ManagedPortKind.CurrentCodex => Resource("PortStatusCurrentCodex"),
            TcpListenerVerifier.ManagedPortKind.PreviousCodex => Resource("PortStatusPreviousCodex"),
            TcpListenerVerifier.ManagedPortKind.NonLoopback => Resource("PortStatusNonLoopback"),
            TcpListenerVerifier.ManagedPortKind.Unreadable => Resource("PortStatusUnreadable"),
            _ => Resource("PortStatusOtherProcess")
        };
        var detail = inspection.ProcessId is null
            ? Resource("PortDetailAvailable")
            : $"PID {inspection.ProcessId} · {inspection.PackageFullName ?? inspection.ExecutablePath ?? Resource("PortDetailUnknown")}";
        return new(
            $"{inspection.Address}:{inspection.Port}",
            detail,
            status,
            inspection.CanTerminate,
            inspection);
    }

    private string Resource(string key) => _resources.GetString(key);

    private sealed record PortRow(
        string Title,
        string Detail,
        string Status,
        bool CanTerminate,
        TcpListenerVerifier.ManagedPortInspection Inspection);
}
