using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CodexDreamSkin.Services;

/// <summary>
/// Owns the native notification-area icon for both packaged and portable builds.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const uint TrayIconId = 1;
    private const uint TrayCallbackMessage = 0x8000 + 0x51;
    private const uint TraySubclassId = 2;

    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifShowTip = 0x00000080;
    private const uint NotifyIconVersion4 = 4;

    private const uint WmNull = 0x0000;
    private const uint WmContextMenu = 0x007B;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const uint WmRButtonUp = 0x0205;

    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmNoNotify = 0x0080;
    private const uint TpmReturnCommand = 0x0100;

    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrDefaultSize = 0x00000040;

    private const uint CommandOpenManager = 1001;
    private const uint CommandOpenThemes = 1002;
    private const uint CommandOpenDiagnostics = 1003;
    private const uint CommandOpenSettings = 1004;
    private const uint CommandHide = 1005;
    private const uint CommandExit = 1006;

    private readonly nint _windowHandle;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Action _showManager;
    private readonly Action<string> _showDestination;
    private readonly Action _hideManager;
    private readonly Func<Task> _exitManager;
    private readonly ResourceLoader _resources = new();
    private readonly SubclassProc _subclassProc;
    private readonly uint _taskbarCreatedMessage;
    private nint _iconHandle;
    private bool _registered;
    private bool _disposed;

    public TrayIconService(
        MainWindow window,
        Action showManager,
        Action<string> showDestination,
        Action hideManager,
        Func<Task> exitManager)
    {
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _dispatcherQueue = window.DispatcherQueue;
        _showManager = showManager;
        _showDestination = showDestination;
        _hideManager = hideManager;
        _exitManager = exitManager;
        _subclassProc = WindowSubclassProc;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

        if (_windowHandle == 0)
        {
            throw new InvalidOperationException("无法获取托盘图标所需的主窗口句柄。");
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _iconHandle = LoadImage(
            0,
            iconPath,
            ImageIcon,
            0,
            0,
            LrLoadFromFile | LrDefaultSize);
        if (_iconHandle == 0)
        {
            throw new InvalidOperationException($"无法加载托盘图标：{iconPath}");
        }

        if (!SetWindowSubclass(_windowHandle, _subclassProc, TraySubclassId, 0))
        {
            DestroyIcon(_iconHandle);
            _iconHandle = 0;
            throw new InvalidOperationException("无法安装托盘消息处理器。");
        }

        try
        {
            AddIcon();
        }
        catch
        {
            RemoveWindowSubclass(_windowHandle, _subclassProc, TraySubclassId);
            DestroyIcon(_iconHandle);
            _iconHandle = 0;
            throw;
        }
    }

    public bool IsRegistered => _registered;

    private void AddIcon()
    {
        var data = CreateNotifyIconData();
        if (!Shell_NotifyIcon(NimAdd, ref data))
        {
            throw new InvalidOperationException("Windows 通知区域拒绝注册应用图标。");
        }

        data.uTimeoutOrVersion = NotifyIconVersion4;
        Shell_NotifyIcon(NimSetVersion, ref data);
        _registered = true;
    }

    private NotifyIconData CreateNotifyIconData() =>
        new()
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = TrayIconId,
            uFlags = NifMessage | NifIcon | NifTip | NifShowTip,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _iconHandle,
            szTip = Resource("TrayTooltip", "Codex 梦幻皮肤"),
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };

    private nint WindowSubclassProc(
        nint windowHandle,
        uint message,
        nuint wordParameter,
        nint longParameter,
        nuint subclassId,
        nuint referenceData)
    {
        if (message == _taskbarCreatedMessage && !_disposed)
        {
            _registered = false;
            try
            {
                AddIcon();
            }
            catch
            {
                // Explorer may still be rebuilding its notification area. The
                // main window remains usable even if this recovery attempt fails.
            }
        }
        else if (message == TrayCallbackMessage)
        {
            var mouseMessage = unchecked((uint)(nuint)longParameter) & 0xFFFF;
            if (mouseMessage == WmLButtonDoubleClick)
            {
                Enqueue(_showManager);
            }
            else if (mouseMessage is WmRButtonUp or WmContextMenu)
            {
                ShowContextMenu();
            }
        }

        return DefSubclassProc(
            windowHandle,
            message,
            wordParameter,
            longParameter);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        try
        {
            AppendMenu(menu, MfString, CommandOpenManager, Resource("TrayOpenManager", "打开管理器"));
            AppendMenu(menu, MfString, CommandOpenThemes, Resource("TrayOpenThemes", "主题"));
            AppendMenu(menu, MfString, CommandOpenDiagnostics, Resource("TrayOpenDiagnostics", "诊断"));
            AppendMenu(menu, MfString, CommandOpenSettings, Resource("TrayOpenSettings", "设置"));
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, CommandHide, Resource("TrayHide", "隐藏窗口"));
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, CommandExit, Resource("TrayExit", "退出"));

            if (!GetCursorPos(out var point))
            {
                return;
            }

            SetForegroundWindow(_windowHandle);
            var command = TrackPopupMenuEx(
                menu,
                TpmRightButton | TpmNoNotify | TpmReturnCommand,
                point.X,
                point.Y,
                _windowHandle,
                0);
            PostMessage(_windowHandle, WmNull, 0, 0);
            DispatchCommand(command);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void DispatchCommand(uint command)
    {
        switch (command)
        {
            case CommandOpenManager:
                Enqueue(_showManager);
                break;
            case CommandOpenThemes:
                Enqueue(() => _showDestination("themes"));
                break;
            case CommandOpenDiagnostics:
                Enqueue(() => _showDestination("diagnostics"));
                break;
            case CommandOpenSettings:
                Enqueue(() => _showDestination("settings"));
                break;
            case CommandHide:
                Enqueue(_hideManager);
                break;
            case CommandExit:
                Enqueue(() => _ = _exitManager());
                break;
        }
    }

    private void Enqueue(Action action)
    {
        if (!_disposed)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!_disposed)
                {
                    action();
                }
            });
        }
    }

    private string Resource(string key, string fallback)
    {
        var value = _resources.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registered)
        {
            var data = CreateNotifyIconData();
            Shell_NotifyIcon(NimDelete, ref data);
            _registered = false;
        }

        RemoveWindowSubclass(_windowHandle, _subclassProc, TraySubclassId);
        if (_iconHandle != 0)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    private delegate nint SubclassProc(
        nint windowHandle,
        uint message,
        nuint wordParameter,
        nint longParameter,
        nuint subclassId,
        nuint referenceData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(
        nint instance,
        string name,
        uint type,
        int desiredWidth,
        int desiredHeight,
        uint load);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint menu, uint flags, nuint itemId, string? text);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        nint menu,
        uint flags,
        int x,
        int y,
        nint windowHandle,
        nint parameters);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        nint windowHandle,
        uint message,
        nuint wordParameter,
        nint longParameter);

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
