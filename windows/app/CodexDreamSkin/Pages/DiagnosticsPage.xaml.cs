using CodexDreamSkin.Models;
using CodexDreamSkin.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodexDreamSkin.Pages;

public sealed partial class DiagnosticsPage : Page
{
    private readonly CodexThemeEngine _engine;

    public DiagnosticsPage()
    {
        InitializeComponent();
        _engine = ((App)Application.Current).ThemeEngine;
    }

    private async void DiagnosticsPage_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        var snapshot = await _engine.InspectAsync();
        PackageBodyText.Text = snapshot.PackageVersion is null
            ? "未找到已注册的 OpenAI.Codex 包。"
            : $"已验证 OpenAI.Codex {snapshot.PackageVersion} 的注册包路径。";
        PackageIcon.Symbol = snapshot.PackageVersion is null ? Symbol.Important : Symbol.Accept;
        RuntimeBodyText.Text = snapshot.ListenerProcessId is null
            ? snapshot.Detail
            : $"可信回环端口 127.0.0.1:{CodexThemeEngine.DefaultPort}，PID {snapshot.ListenerProcessId}，目标 {snapshot.TargetCount}。";
        RuntimeIcon.Symbol = snapshot.State switch
        {
            EngineState.Active => Symbol.Accept,
            EngineState.Faulted => Symbol.Important,
            _ => Symbol.Clock
        };
    }
}
