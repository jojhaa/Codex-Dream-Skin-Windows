using System.Diagnostics;
using CodexDreamSkin.Models;

namespace CodexDreamSkin.Services;

public sealed record CodexCloseResult(bool Succeeded, int ProcessCount, string Detail);

public sealed class CodexProcessController
{
    private readonly CodexPackageLocator _packageLocator = new();

    public async Task<CodexCloseResult> CloseCurrentPackageAsync(
        CodexInstallation expectedInstallation,
        CancellationToken cancellationToken)
    {
        var current = _packageLocator.FindCurrent();
        if (current is null ||
            !string.Equals(current.PackageFullName, expectedInstallation.PackageFullName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetFullPath(current.ExecutablePath),
                Path.GetFullPath(expectedInstallation.ExecutablePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false, 0, "Codex 包身份在接管前发生变化，已停止操作。");
        }

        var initial = CodexPackageLocator.FindRunningProcesses(current);
        var processCount = initial.Count;
        try
        {
            foreach (var process in initial)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (process.MainWindowHandle != 0)
                    {
                        process.CloseMainWindow();
                    }
                }
                catch
                {
                    // A short-lived Electron child can exit while identities are being inspected.
                }
            }
        }
        finally
        {
            foreach (var process in initial)
            {
                process.Dispose();
            }
        }

        if (await WaitForExitAsync(current, TimeSpan.FromSeconds(8), cancellationToken))
        {
            return new(true, processCount, "Codex 已正常关闭。");
        }

        current = _packageLocator.FindCurrent();
        if (current is null ||
            !string.Equals(current.PackageFullName, expectedInstallation.PackageFullName, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, processCount, "Codex 包身份在结束残留进程前发生变化，已停止操作。");
        }

        var remaining = CodexPackageLocator.FindRunningProcesses(current);
        try
        {
            foreach (var process in remaining)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var identity = ProcessPathResolver.TryGetIdentity(process.Id);
                if (!string.Equals(identity?.PackageFullName, current.PackageFullName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        identity?.ExecutablePath is null ? null : Path.GetFullPath(identity.ExecutablePath),
                        Path.GetFullPath(current.ExecutablePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Report the remaining verified processes after the bounded wait below.
                }
            }
        }
        finally
        {
            foreach (var process in remaining)
            {
                process.Dispose();
            }
        }

        var stopped = await WaitForExitAsync(current, TimeSpan.FromSeconds(6), cancellationToken);
        return stopped
            ? new(true, processCount, "Codex 已关闭，残留的官方包进程也已清理。")
            : new(false, processCount, "部分 Codex 进程仍在运行；未启动新的主题会话。");
    }

    private static async Task<bool> WaitForExitAsync(
        CodexInstallation installation,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processes = CodexPackageLocator.FindRunningProcesses(installation);
            try
            {
                if (processes.Count == 0)
                {
                    return true;
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        return false;
    }
}
