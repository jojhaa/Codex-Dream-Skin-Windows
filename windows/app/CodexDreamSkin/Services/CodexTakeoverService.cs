using CodexDreamSkin.Models;

namespace CodexDreamSkin.Services;

public sealed class CodexTakeoverService : IAsyncDisposable
{
    private readonly ManagerSettingsService _settings;
    private readonly CodexThemeEngine _engine;
    private readonly CodexPackageLocator _packageLocator = new();
    private readonly CodexProcessController _processController = new();
    private CancellationTokenSource? _cancellation;
    private Task? _watchTask;
    private string? _candidateFingerprint;
    private int _candidateObservations;
    private DateTimeOffset _cooldownUntil;

    public TakeoverSnapshot Snapshot { get; private set; } = TakeoverSnapshot.Disabled;
    public event EventHandler<TakeoverSnapshot>? SnapshotChanged;

    public CodexTakeoverService(ManagerSettingsService settings, CodexThemeEngine engine)
    {
        _settings = settings;
        _engine = engine;
    }

    public void Start()
    {
        if (_watchTask is not null || !_settings.AutoTakeoverEnabled)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        Publish(new(
            TakeoverState.Watching,
            "正在监听普通 Codex 启动",
            "只会接管当前 Microsoft Store 签名的 Codex 包。",
            DateTimeOffset.UtcNow));
        _watchTask = WatchAsync(_cancellation.Token);
    }

    public async Task StopAsync()
    {
        _cancellation?.Cancel();
        if (_watchTask is not null)
        {
            try
            {
                await _watchTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _watchTask = null;
        _cancellation?.Dispose();
        _cancellation = null;
        ResetCandidate();
        Publish(TakeoverSnapshot.Disabled);
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!_settings.AutoTakeoverEnabled)
                {
                    await StopFromWatcherAsync();
                    return;
                }

                await InspectAndTakeOverAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Publish(new(
                TakeoverState.Faulted,
                "普通启动托管已暂停",
                error.Message,
                DateTimeOffset.UtcNow));
        }
    }

    private async Task InspectAndTakeOverAsync(CancellationToken cancellationToken)
    {
        var installation = _packageLocator.FindCurrent();
        if (installation is null)
        {
            ResetCandidate();
            Publish(new(
                TakeoverState.Watching,
                "等待安装 Codex",
                "未发现 Microsoft Store Codex 包。",
                DateTimeOffset.UtcNow));
            return;
        }

        var trustedListener = _engine.FindTrustedListener(installation);
        if (trustedListener is not null)
        {
            ResetCandidate();
            if (_engine.Snapshot.State != EngineState.Active ||
                _engine.Snapshot.ListenerPort != trustedListener.Value.Port)
            {
                var attached = await _engine.StartOrApplyAsync(cancellationToken);
                Publish(attached.State == EngineState.Active
                    ? new(
                        TakeoverState.Active,
                        "普通启动托管运行中",
                        $"已重新连接 Codex {installation.Version} 的可信主题端口 {trustedListener.Value.Port}。",
                        DateTimeOffset.UtcNow)
                    : new(
                        TakeoverState.Faulted,
                        "可信主题端口重新连接失败",
                        attached.Detail,
                        DateTimeOffset.UtcNow));
            }
            else if (Snapshot.State != TakeoverState.Active)
            {
                Publish(new(
                    TakeoverState.Active,
                    "普通启动托管运行中",
                    $"Codex {installation.Version} 已通过可信主题端口运行。",
                    DateTimeOffset.UtcNow));
            }
            return;
        }

        var processes = CodexPackageLocator.FindRunningProcesses(installation);
        string fingerprint;
        try
        {
            if (processes.Count == 0)
            {
                ResetCandidate();
                if (Snapshot.State != TakeoverState.Watching)
                {
                    Publish(new(
                        TakeoverState.Watching,
                        "正在监听普通 Codex 启动",
                        $"已识别 Codex {installation.Version}，等待普通启动。",
                        DateTimeOffset.UtcNow));
                }
                return;
            }

            fingerprint = string.Join(",", processes.Select(process => process.Id).Order());
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        if (DateTimeOffset.UtcNow < _cooldownUntil)
        {
            return;
        }

        if (!string.Equals(fingerprint, _candidateFingerprint, StringComparison.Ordinal))
        {
            _candidateFingerprint = fingerprint;
            _candidateObservations = 1;
            return;
        }

        _candidateObservations++;
        if (_candidateObservations < 2)
        {
            return;
        }

        ResetCandidate();
        Publish(new(
            TakeoverState.TakingOver,
            "正在接管普通 Codex 启动",
            "正在安全关闭无主题端口的官方 Codex；未发送内容可能会丢失。",
            DateTimeOffset.UtcNow));

        var closed = await _processController.CloseCurrentPackageAsync(installation, cancellationToken);
        if (!closed.Succeeded)
        {
            _cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(15);
            Publish(new(
                TakeoverState.Faulted,
                "无法接管普通 Codex",
                closed.Detail,
                DateTimeOffset.UtcNow));
            return;
        }

        await Task.Delay(500, cancellationToken);
        var result = await _engine.StartOrApplyAsync(cancellationToken);
        _cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(5);
        Publish(result.State == EngineState.Active
            ? new(
                TakeoverState.Active,
                "普通启动已自动应用主题",
                $"{result.Summary}（Codex {result.PackageVersion}，端口 {result.ListenerPort}）",
                DateTimeOffset.UtcNow)
            : new(
                TakeoverState.Faulted,
                "Codex 已重启，但主题未完成",
                result.Detail,
                DateTimeOffset.UtcNow));
    }

    private Task StopFromWatcherAsync()
    {
        _watchTask = null;
        _cancellation?.Dispose();
        _cancellation = null;
        ResetCandidate();
        Publish(TakeoverSnapshot.Disabled);
        return Task.CompletedTask;
    }

    private void ResetCandidate()
    {
        _candidateFingerprint = null;
        _candidateObservations = 0;
    }

    private void Publish(TakeoverSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
