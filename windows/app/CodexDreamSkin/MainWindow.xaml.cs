using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CodexDreamSkin;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int MinimumWindowWidth = 770;
    private const int MinimumWindowHeight = 680;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint MinimumSizeSubclassId = 1;

    private readonly SubclassProc _minimumSizeSubclassProc;
    private nint _windowHandle;
    private bool _isCorrectingWindowSize;
    private bool _allowClose;
    private bool _closeToTrayEnabled;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1240, 800));
        _minimumSizeSubclassProc = MinimumSizeWindowProc;
        InstallMinimumSizeGuard();
        AppWindow.Changed += AppWindow_Changed;
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_allowClose && _closeToTrayEnabled)
        {
            args.Cancel = true;
            HideToBackground();
        }
    }

    public void HideToBackground() => AppWindow.Hide();

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
    }

    public void ShowDestination(string tag)
    {
        ShowAndActivate();
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.NavigateTo(tag);
        }
    }

    public void EnableCloseToTray() => _closeToTrayEnabled = true;

    public void AllowClose() => _allowClose = true;

    private void InstallMinimumSizeGuard()
    {
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (_windowHandle == 0 ||
            !SetWindowSubclass(_windowHandle, _minimumSizeSubclassProc, MinimumSizeSubclassId, 0))
        {
            throw new InvalidOperationException("无法安装窗口最小尺寸保护。");
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange || _isCorrectingWindowSize)
        {
            return;
        }

        var currentSize = sender.Size;
        var correctedWidth = Math.Max(currentSize.Width, MinimumWindowWidth);
        var correctedHeight = Math.Max(currentSize.Height, MinimumWindowHeight);
        if (correctedWidth == currentSize.Width && correctedHeight == currentSize.Height)
        {
            return;
        }

        _isCorrectingWindowSize = true;
        try
        {
            sender.Resize(new SizeInt32(correctedWidth, correctedHeight));
        }
        finally
        {
            _isCorrectingWindowSize = false;
        }
    }

    private nint MinimumSizeWindowProc(
        nint windowHandle,
        uint message,
        nuint wordParameter,
        nint longParameter,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == WmGetMinMaxInfo && longParameter != 0)
        {
            var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(longParameter);
            minMaxInfo.MinimumTrackSize.X = Math.Max(
                minMaxInfo.MinimumTrackSize.X,
                MinimumWindowWidth);
            minMaxInfo.MinimumTrackSize.Y = Math.Max(
                minMaxInfo.MinimumTrackSize.Y,
                MinimumWindowHeight);
            Marshal.StructureToPtr(minMaxInfo, longParameter, false);
        }

        return DefSubclassProc(windowHandle, message, wordParameter, longParameter);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        AppWindow.Changed -= AppWindow_Changed;
        AppWindow.Closing -= AppWindow_Closing;
        if (_windowHandle != 0)
        {
            RemoveWindowSubclass(_windowHandle, _minimumSizeSubclassProc, MinimumSizeSubclassId);
            _windowHandle = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaximumSize;
        public Point MaximumPosition;
        public Point MinimumTrackSize;
        public Point MaximumTrackSize;
    }

    private delegate nint SubclassProc(
        nint windowHandle,
        uint message,
        nuint wordParameter,
        nint longParameter,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint windowHandle,
        SubclassProc subclassProc,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        nint windowHandle,
        SubclassProc subclassProc,
        nuint subclassId);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(
        nint windowHandle,
        uint message,
        nuint wordParameter,
        nint longParameter);
}
