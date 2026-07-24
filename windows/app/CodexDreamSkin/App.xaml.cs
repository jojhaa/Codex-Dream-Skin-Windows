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
    public ReleaseCheckService ReleaseChecks { get; } = new();
    public CodexTakeoverService TakeoverService { get; }
    private TrayIconService? _trayIcon;
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
        if (MainWindow is MainWindow managerWindow)
        {
            try
            {
                _trayIcon = new TrayIconService(
                    managerWindow,
                    managerWindow.ShowAndActivate,
                    managerWindow.ShowDestination,
                    managerWindow.HideToBackground,
                    ExitManagerAsync);
                if (_trayIcon.IsRegistered)
                {
                    managerWindow.EnableCloseToTray();
                }
            }
            catch
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
            }
        }

        TakeoverService.Start();

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var isStartupLaunch =
            activation.Kind == ExtendedActivationKind.StartupTask ||
            string.Equals(
                args.Arguments?.Trim(),
                StartupTaskService.PortableStartupArgument,
                StringComparison.OrdinalIgnoreCase);
        if (isStartupLaunch &&
            ManagerSettings.AutoTakeoverEnabled &&
            MainWindow is MainWindow startupWindow)
        {
            startupWindow.HideToBackground();
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
        _trayIcon?.Dispose();
        _trayIcon = null;
        await TakeoverService.DisposeAsync();
        await ThemeEngine.DisposeAsync();
    }
}
