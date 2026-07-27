using CodexDreamSkin.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CodexDreamSkin;

public sealed partial class MainPage : Page
{
    private readonly ResourceLoader _resources = new();

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        NavigateTo("themes");
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            NavigateTo(tag);
        }
    }

    public void NavigateTo(string tag)
    {
        var normalizedTag = tag.ToLowerInvariant();
        var pageType = normalizedTag switch
        {
            "themes" => typeof(ThemesPage),
            "diagnostics" => typeof(DiagnosticsPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(DashboardPage),
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }

        var resourceSuffix = normalizedTag switch
        {
            "themes" => "Themes",
            "diagnostics" => "Diagnostics",
            "settings" => "Settings",
            _ => "Dashboard",
        };
        WorkspaceModeContext.Text = _resources.GetString($"WorkspaceContext{resourceSuffix}");

        DashboardModeButton.Style = normalizedTag == "dashboard"
            ? (Style)Application.Current.Resources["AccentButtonStyle"]
            : null;
        ThemesModeButton.Style = normalizedTag == "themes"
            ? (Style)Application.Current.Resources["AccentButtonStyle"]
            : null;
        DiagnosticsModeButton.Style = normalizedTag == "diagnostics"
            ? (Style)Application.Current.Resources["AccentButtonStyle"]
            : null;
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        WorkspaceModeContext.Visibility = e.NewSize.Width >= 1120
            ? Visibility.Visible
            : Visibility.Collapsed;
        DiagnosticsModeButton.Visibility = e.NewSize.Width >= 860
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
