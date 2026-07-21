using System.Diagnostics;
using System.Text.Json;
using CodexDreamSkin.Models;

namespace CodexDreamSkin.Services;

public sealed class CodexThemeEngine : IAsyncDisposable
{
    public const int DefaultPort = 9335;
    private readonly CodexPackageLocator _packageLocator = new();
    private readonly TcpListenerVerifier _listenerVerifier = new();
    private readonly ThemePayloadLoader _payloadLoader = new();
    private readonly ThemeCatalogService _themeCatalog;
    private readonly Dictionary<string, CdpSession> _sessions = [];
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _watchCancellation;
    private Task? _watchTask;
    private CdpClient? _client;
    private string? _browserId;
    private ThemePayload? _payload;

    public EngineSnapshot Snapshot { get; private set; } = EngineSnapshot.Idle;
    public event EventHandler<EngineSnapshot>? SnapshotChanged;

    public CodexThemeEngine(ThemeCatalogService themeCatalog) => _themeCatalog = themeCatalog;

    public async Task<EngineSnapshot> InspectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try { return InspectCore(cancellationToken); }
        finally { _operationGate.Release(); }
    }

    public async Task<EngineSnapshot> StartOrApplyAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var installation = _packageLocator.FindCurrent();
            if (installation is null)
                return Publish(new(EngineState.CodexNotInstalled, "未发现 Codex", "请先从 Microsoft Store 安装 Codex。"));

            var owner = _listenerVerifier.GetTrustedLoopbackOwner(DefaultPort, installation);
            if (owner is null)
            {
                var running = CodexPackageLocator.FindRunningProcesses(installation);
                try
                {
                    if (running.Count > 0)
                        return Publish(new(EngineState.RestartRequired, "需要重启 Codex", "Codex 正在运行，但没有可信的主题调试端口。请关闭 Codex 后再次点击应用；管理器不会强制结束你的会话。", installation.Version));
                }
                finally { foreach (var process in running) process.Dispose(); }

                if (_listenerVerifier.IsOccupied(DefaultPort))
                {
                    var inspection = _listenerVerifier.Inspect(DefaultPort, installation);
                    return Publish(new(EngineState.Faulted, "端口监听器验证失败", FormatInspection(inspection), installation.Version));
                }

                Publish(new(EngineState.Connecting, "正在启动 Codex", "使用仅限回环地址的 CDP 端口启动。", installation.Version));
                var startInfo = new ProcessStartInfo { FileName = installation.ExecutablePath, UseShellExecute = false };
                startInfo.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
                startInfo.ArgumentList.Add($"--remote-debugging-port={DefaultPort}");
                Process.Start(startInfo)?.Dispose();

                var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(400, cancellationToken);
                    owner = _listenerVerifier.GetTrustedLoopbackOwner(DefaultPort, installation);
                    if (owner is not null) break;
                }
                if (owner is null)
                    return Publish(new(EngineState.Faulted, "Codex 未开放可信端口", "45 秒内未检测到由 Codex 包拥有的回环监听器。", installation.Version));
            }

            Publish(new(EngineState.Connecting, "正在连接主题引擎", "正在验证浏览器实例与 Codex 页面。", installation.Version, owner));
            await ConnectAndApplyCoreAsync(installation, owner.Value, cancellationToken);
            return Snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { return Publish(new(EngineState.Faulted, "主题应用失败", error.Message)); }
        finally { _operationGate.Release(); }
    }

    private EngineSnapshot InspectCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var installation = _packageLocator.FindCurrent();
        if (installation is null) return Publish(new(EngineState.CodexNotInstalled, "未发现 Codex", "请先安装 Codex。"));

        var owner = _listenerVerifier.GetTrustedLoopbackOwner(DefaultPort, installation);
        if (owner is not null)
        {
            var active = _sessions.Count > 0;
            return Publish(new(active ? EngineState.Active : EngineState.Idle,
                active ? "主题引擎运行中" : "Codex 已准备好",
                active ? $"已连接 {_sessions.Count} 个经过验证的 Codex 页面。" : "检测到可信回环端口，可以应用主题。",
                installation.Version, owner, _sessions.Count));
        }

        if (_listenerVerifier.IsOccupied(DefaultPort))
        {
            var inspection = _listenerVerifier.Inspect(DefaultPort, installation);
            return Publish(new(EngineState.Faulted, "发现不可信监听器", FormatInspection(inspection), installation.Version));
        }

        var processes = CodexPackageLocator.FindRunningProcesses(installation);
        try
        {
            return Publish(new(processes.Count > 0 ? EngineState.RestartRequired : EngineState.Idle,
                processes.Count > 0 ? "需要重启 Codex" : "可以启动主题会话",
                processes.Count > 0 ? "当前 Codex 没有主题端口；关闭它后即可由管理器安全启动。" : "Codex 当前未运行，点击应用主题即可启动。",
                installation.Version));
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private async Task ConnectAndApplyCoreAsync(CodexInstallation installation, int owner, CancellationToken cancellationToken)
    {
        await StopWatcherCoreAsync();
        _client = new CdpClient(DefaultPort);
        var endpoint = await _client.GetEndpointAsync(cancellationToken);
        _browserId = endpoint.BrowserId;
        _payload = await _payloadLoader.LoadAsync(await _themeCatalog.GetActiveThemeDirectoryAsync(cancellationToken), cancellationToken);
        foreach (var target in await _client.GetAppTargetsAsync(_browserId, cancellationToken)) await AttachTargetAsync(target, cancellationToken);
        if (_sessions.Count == 0) throw new InvalidOperationException("未找到同时具有 Codex 主界面标记的 app:// 页面。");

        Publish(new(EngineState.Active, "主题已应用", $"“{_payload.ThemeName}”已连接 {_sessions.Count} 个页面；页面重载后会自动恢复。", installation.Version, owner, _sessions.Count));
        _watchCancellation = new CancellationTokenSource();
        _watchTask = WatchTargetsAsync(installation, owner, _watchCancellation.Token);
    }

    private async Task AttachTargetAsync(CdpTarget target, CancellationToken cancellationToken)
    {
        if (_sessions.ContainsKey(target.Id) || _payload is null) return;
        var session = new CdpSession();
        try
        {
            await session.OpenAsync(target.WebSocketUrl, cancellationToken);
            var probe = await session.EvaluateAsync("(() => { const root=!!document.getElementById('root'); const shell=!!document.querySelector('main.main-surface'); const sidebar=!!document.querySelector('aside.app-shell-left-panel'); return { codex: location.protocol === 'app:' && root && shell && sidebar }; })()", cancellationToken);
            if (probe.ValueKind != JsonValueKind.Object || !probe.TryGetProperty("codex", out var codexNode) || !codexNode.GetBoolean())
            {
                await session.DisposeAsync();
                return;
            }
            var early = ThemePayloadLoader.BuildEarlyExpression(_payload);
            await session.SendAsync("Page.addScriptToEvaluateOnNewDocument", new { source = early }, cancellationToken);
            await session.EvaluateAsync(_payload.Expression, cancellationToken);
            session.EventReceived += async (method, _) =>
            {
                if (method == "Page.loadEventFired" && _payload is not null)
                    try { await session.EvaluateAsync(ThemePayloadLoader.BuildEarlyExpression(_payload), _watchCancellation?.Token ?? CancellationToken.None); } catch { }
            };
            _sessions[target.Id] = session;
        }
        catch { await session.DisposeAsync(); throw; }
    }

    private async Task WatchTargetsAsync(CodexInstallation installation, int owner, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_listenerVerifier.GetTrustedLoopbackOwner(DefaultPort, installation) != owner || _client is null || _browserId is null)
                    throw new InvalidOperationException("可信 Codex 监听器已退出或被替换。");
                foreach (var closed in _sessions.Where(item => item.Value.IsClosed).Select(item => item.Key).ToArray())
                {
                    await _sessions[closed].DisposeAsync();
                    _sessions.Remove(closed);
                }
                foreach (var target in await _client.GetAppTargetsAsync(_browserId, cancellationToken))
                    if (!_sessions.ContainsKey(target.Id)) try { await AttachTargetAsync(target, cancellationToken); } catch { }
                Publish(Snapshot with { Detail = $"“{_payload?.ThemeName}”已连接 {_sessions.Count} 个页面；页面重载后会自动恢复。", TargetCount = _sessions.Count });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { Publish(new(EngineState.Faulted, "主题连接已中断", error.Message, installation.Version)); }
    }

    private EngineSnapshot Publish(EngineSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
        return snapshot;
    }

    private static string FormatInspection(TcpListenerVerifier.ListenerInspection inspection) =>
        $"{inspection.Reason} PID {inspection.ObservedProcessId?.ToString() ?? "未知"}。实际路径：{inspection.ObservedPath ?? "不可用"}。期望路径：{inspection.ExpectedPath}";

    private async Task StopWatcherCoreAsync()
    {
        _watchCancellation?.Cancel();
        if (_watchTask is not null) try { await _watchTask; } catch { }
        foreach (var session in _sessions.Values) await session.DisposeAsync();
        _sessions.Clear();
        _client?.Dispose();
        _client = null;
        _browserId = null;
        _watchTask = null;
        _watchCancellation?.Dispose();
        _watchCancellation = null;
    }

    public async ValueTask DisposeAsync()
    {
        await _operationGate.WaitAsync();
        try { await StopWatcherCoreAsync(); }
        finally { _operationGate.Release(); _operationGate.Dispose(); }
    }
}
