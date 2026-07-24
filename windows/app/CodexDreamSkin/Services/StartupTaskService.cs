using Microsoft.Win32;
using Windows.ApplicationModel;

namespace CodexDreamSkin.Services;

public sealed class StartupTaskService
{
    public const string TaskId = "CodexDreamSkinMonitor";
    public const string PortableStartupArgument = "--startup";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "CodexDreamSkin";

    public async Task<StartupTaskState?> GetStateAsync()
    {
        if (!HasPackageIdentity())
        {
            return GetPortableState();
        }

        try
        {
            return (await StartupTask.GetAsync(TaskId)).State;
        }
        catch
        {
            return null;
        }
    }

    public async Task<StartupTaskState?> SetEnabledAsync(bool enabled)
    {
        if (!HasPackageIdentity())
        {
            return SetPortableEnabled(enabled);
        }

        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (!enabled)
            {
                task.Disable();
                return task.State;
            }

            return task.State == StartupTaskState.Enabled
                ? task.State
                : await task.RequestEnableAsync();
        }
        catch
        {
            return null;
        }
    }

    private static bool HasPackageIdentity()
    {
        try
        {
            _ = Package.Current.Id.FullName;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static StartupTaskState? GetPortableState()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var registeredCommand = runKey?.GetValue(RunValueName) as string;
            return string.Equals(
                registeredCommand,
                BuildPortableCommand(),
                StringComparison.OrdinalIgnoreCase)
                ? StartupTaskState.Enabled
                : StartupTaskState.Disabled;
        }
        catch
        {
            return null;
        }
    }

    private static StartupTaskState? SetPortableEnabled(bool enabled)
    {
        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                runKey.SetValue(
                    RunValueName,
                    BuildPortableCommand(),
                    RegistryValueKind.String);
                return StartupTaskState.Enabled;
            }

            runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
            return StartupTaskState.Disabled;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPortableCommand()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) ||
            !File.Exists(executablePath))
        {
            throw new InvalidOperationException("无法确定当前主题管理器的可执行文件路径。");
        }

        return $"\"{Path.GetFullPath(executablePath)}\" {PortableStartupArgument}";
    }
}
