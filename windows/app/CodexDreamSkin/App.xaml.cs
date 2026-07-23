using CodexDreamSkin.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace CodexDreamSkin;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public Window? MainWindow { get; private set; }
    public ThemeCatalogService ThemeCatalog { get; } = new();
    public CodexThemeEngine ThemeEngine { get; }
    public ManagerSettingsService ManagerSettings { get; } = new();
    public StartupTaskService StartupTasks { get; } = new();
    public CodexTakeoverService TakeoverService { get; }
    private bool _servicesDisposed;

    public App()
    {
        ThemeEngine = new CodexThemeEngine(ThemeCatalog);
        TakeoverService = new CodexTakeoverService(ManagerSettings, ThemeEngine);
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Closed += async (_, _) => await DisposeServicesAsync();
        AppInstance.GetCurrent().Activated += CurrentInstance_Activated;

        if (MainWindow.Content is FrameworkElement root)
        {
            root.RequestedTheme = ManagerSettings.Appearance switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }

        MainWindow.Activate();
        TakeoverService.Start();

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activation.Kind == ExtendedActivationKind.StartupTask &&
            ManagerSettings.AutoTakeoverEnabled &&
            MainWindow is MainWindow managerWindow)
        {
            managerWindow.HideToBackground();
        }
    }

    private void CurrentInstance_Activated(object? sender, AppActivationArguments args)
    {
        MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            if (MainWindow is MainWindow managerWindow)
            {
                managerWindow.ShowAndActivate();
            }
        });
    }

    public async Task ExitManagerAsync()
    {
        if (MainWindow is MainWindow managerWindow)
        {
            managerWindow.AllowClose();
        }

        await DisposeServicesAsync();
        MainWindow?.Close();
    }

    private async Task DisposeServicesAsync()
    {
        if (_servicesDisposed)
        {
            return;
        }

        _servicesDisposed = true;
        AppInstance.GetCurrent().Activated -= CurrentInstance_Activated;
        await TakeoverService.DisposeAsync();
        await ThemeEngine.DisposeAsync();
    }
}
