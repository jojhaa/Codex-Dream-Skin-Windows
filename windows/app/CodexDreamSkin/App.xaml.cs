using Windows.ApplicationModel;
using CodexDreamSkin.Services;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CodexDreamSkin;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public Window? MainWindow { get; private set; }
    public ThemeCatalogService ThemeCatalog { get; } = new();
    public CodexThemeEngine ThemeEngine { get; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        ThemeEngine = new CodexThemeEngine(ThemeCatalog);
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Closed += async (_, _) => await ThemeEngine.DisposeAsync();
        MainWindow.Activate();
    }
}
