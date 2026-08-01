using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;
using CodexDreamSkin.Models;

namespace CodexDreamSkin.Services;

public sealed class CodexThemeEngine : IAsyncDisposable
{
    public const int DefaultPort = 9335;
    public const int LastManagedPort = 9345;
    private const string DreamCommandBinding = "__dreamSkinCommand";
    private readonly CodexPackageLocator _packageLocator = new();
    private readonly TcpListenerVerifier _listenerVerifier = new();
    private readonly ThemePayloadLoader _payloadLoader = new();
    private readonly ThemeCatalogService _themeCatalog;
    private readonly Dictionary<string, CdpSession> _sessions = [];
    private readonly Dictionary<string, string> _earlyScriptIdentifiers = [];
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _watchCancellation;
    private Task? _watchTask;
    private CdpClient? _client;
    private string? _browserId;
    private ThemePayload? _payload;
    private bool _isPreviewPayload;

    public EngineSnapshot Snapshot { get; private set; } = EngineSnapshot.Idle;
    public event EventHandler<EngineSnapshot>? SnapshotChanged;

    public CodexThemeEngine(ThemeCatalogService themeCatalog) => _themeCatalog = themeCatalog;

    public async Task<EngineSnapshot> InspectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try { return InspectCore(cancellationToken); }
        finally { _operationGate.Release(); }
    }

    public Task<EngineSnapshot> StartOrApplyAsync(CancellationToken cancellationToken = default) =>
        StartOrApplyThemeAsync(null, false, cancellationToken);

    public async Task<ThemeRegionMetrics?> MeasureRegionsAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            foreach (var session in _sessions.Values.Where(value => !value.IsClosed))
            {
                var measured = await MeasureRegionsAsync(session, cancellationToken);
                if (measured is not null) return measured;
            }

            var installation = _packageLocator.FindCurrent();
            var listener = installation is null ? null : FindTrustedListener(installation);
            if (installation is null || listener is null)
                return null;
            using var client = new CdpClient(listener.Value.Port);
            var endpoint = await client.GetEndpointAsync(cancellationToken);
            foreach (var target in await client.GetAppTargetsAsync(endpoint.BrowserId, cancellationToken))
            {
                await using var session = new CdpSession();
                try
                {
                    await session.OpenAsync(target.WebSocketUrl, cancellationToken);
                    var measured = await MeasureRegionsAsync(session, cancellationToken);
                    if (measured is not null) return measured;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Auxiliary app:// targets can disappear while Codex changes routes.
                }
            }
            return null;
        }
        finally { _operationGate.Release(); }
    }

    public async Task<CodexPreviewFrame?> CapturePreviewFrameAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (!_isPreviewPayload)
            {
                return null;
            }

            foreach (var session in _sessions.Values.Where(value => !value.IsClosed))
            {
                try
                {
                    var rendered = await session.EvaluateAsync(
                        """
                        new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(() => {
                          resolve({
                            codex: location.protocol === 'app:' &&
                              !!document.querySelector('main.main-surface, main[data-app-shell-main-surface]') &&
                              !!document.getElementById('codex-dream-skin-style'),
                            width: Math.max(1, Math.round(innerWidth)),
                            height: Math.max(1, Math.round(innerHeight))
                          });
                        })))
                        """,
                        cancellationToken);
                    if (rendered.ValueKind != JsonValueKind.Object ||
                        !rendered.TryGetProperty("codex", out var codexNode) ||
                        !codexNode.GetBoolean())
                    {
                        continue;
                    }

                    var width = rendered.TryGetProperty("width", out var widthNode) ? widthNode.GetInt32() : 1;
                    var height = rendered.TryGetProperty("height", out var heightNode) ? heightNode.GetInt32() : 1;
                    var pngBytes = await session.CaptureScreenshotAsync(cancellationToken);
                    return new CodexPreviewFrame(
                        pngBytes,
                        Math.Clamp(width, 1, 16384),
                        Math.Clamp(height, 1, 16384),
                        DateTimeOffset.UtcNow);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // A route-changing auxiliary target may disappear between render and capture.
                }
            }

            return null;
        }
        finally { _operationGate.Release(); }
    }

    public Task<EngineSnapshot> PreviewAsync(string themeDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeDirectory);
        return StartOrApplyThemeAsync(themeDirectory, true, cancellationToken);
    }

    public async Task<EngineSnapshot> RefreshPreviewAsync(string themeDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeDirectory);
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var installation = _packageLocator.FindCurrent();
            if (installation is null)
                return Publish(new(EngineState.CodexNotInstalled, "未发现 Codex", "请先从 Microsoft Store 安装 Codex。"));
            var listener = FindTrustedListener(installation);
            var owner = listener?.ProcessId;
            if (!_isPreviewPayload || _client is null || _browserId is null || _sessions.Count == 0 || owner is null)
                return Publish(new(EngineState.Faulted, "实时预览尚未连接", "请重新开启实时同步，或先使用一次临时预览。", installation.Version));

            var nextPayload = await _payloadLoader.LoadAsync(themeDirectory, cancellationToken);
            foreach (var (targetId, session) in _sessions.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nextIdentifier = await RegisterEarlyScriptAsync(session, nextPayload, cancellationToken);
                try { await session.EvaluateAsync(nextPayload.Expression, cancellationToken); }
                catch
                {
                    await RemoveEarlyScriptAsync(session, nextIdentifier, CancellationToken.None);
                    throw;
                }
                if (_earlyScriptIdentifiers.Remove(targetId, out var previousIdentifier))
                    await RemoveEarlyScriptAsync(session, previousIdentifier, cancellationToken);
                _earlyScriptIdentifiers[targetId] = nextIdentifier;
            }
            _payload = nextPayload;
            return Publish(new(
                EngineState.Active,
                "实时预览已同步",
                $"“{nextPayload.ThemeName}”草稿已热重载到 {_sessions.Count} 个页面。",
                installation.Version,
                owner,
                _sessions.Count,
                listener?.Port));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { return Publish(new(EngineState.Faulted, "实时预览失败", error.Message)); }
        finally { _operationGate.Release(); }
    }

    private async Task<EngineSnapshot> StartOrApplyThemeAsync(string? themeDirectory, bool preview, CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var installation = _packageLocator.FindCurrent();
            if (installation is null)
                return Publish(new(EngineState.CodexNotInstalled, "未发现 Codex", "请先从 Microsoft Store 安装 Codex。"));

            var listener = FindTrustedListener(installation);
            var owner = listener?.ProcessId;
            var port = listener?.Port;
            if (owner is null)
            {
                var running = CodexPackageLocator.FindRunningProcesses(installation);
                try
                {
                    if (running.Count > 0)
                        return Publish(new(EngineState.RestartRequired, "需要重启 Codex", "Codex 正在运行，但没有可信的主题调试端口。请关闭 Codex 后再次点击应用；管理器不会强制结束你的会话。", installation.Version));
                }
                finally { foreach (var process in running) process.Dispose(); }

                port = FindAvailablePort();
                if (port is null)
                    return Publish(new(
                        EngineState.Faulted,
                        "没有可用的主题端口",
                        $"安全端口 {DefaultPort}–{LastManagedPort} 均被占用；关闭旧版 Codex 后重新检查。",
                        installation.Version));

                Publish(new(
                    EngineState.Connecting,
                    "正在启动 Codex",
                    $"已动态解析 Codex {installation.Version}，使用回环端口 {port} 启动。",
                    installation.Version,
                    ListenerPort: port));
                var startInfo = new ProcessStartInfo { FileName = installation.ExecutablePath, UseShellExecute = false };
                startInfo.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
                startInfo.ArgumentList.Add($"--remote-debugging-port={port}");
                Process.Start(startInfo)?.Dispose();

                var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(400, cancellationToken);
                    owner = _listenerVerifier.GetTrustedLoopbackOwner(port.Value, installation);
                    if (owner is not null) break;
                }
                if (owner is null)
                    return Publish(new(EngineState.Faulted, "Codex 未开放可信端口", "45 秒内未检测到由 Codex 包拥有的回环监听器。", installation.Version));
            }

            Publish(new(
                EngineState.Connecting,
                "正在连接主题引擎",
                $"正在验证当前动态包与回环端口 {port}。",
                installation.Version,
                owner,
                ListenerPort: port));
            await ConnectAndApplyCoreAsync(installation, owner.Value, port!.Value, themeDirectory, preview, cancellationToken);
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

        var listener = FindTrustedListener(installation);
        if (listener is not null)
        {
            var active = _sessions.Count > 0;
            return Publish(new(active ? EngineState.Active : EngineState.Idle,
                active ? "主题引擎运行中" : "Codex 已准备好",
                active ? $"已连接 {_sessions.Count} 个经过验证的 Codex 页面。" : "检测到可信回环端口，可以应用主题。",
                installation.Version, listener.Value.ProcessId, _sessions.Count, listener.Value.Port));
        }

        var processes = CodexPackageLocator.FindRunningProcesses(installation);
        try
        {
            return Publish(new(processes.Count > 0 ? EngineState.RestartRequired : EngineState.Idle,
                processes.Count > 0 ? "需要重启 Codex" : "可以启动主题会话",
                processes.Count > 0
                    ? $"已动态检测到 Codex {installation.Version}，但它没有可信主题端口；关闭 Codex 后再次应用即可。"
                    : $"已动态检测到 Codex {installation.Version}；应用时会自动避开旧版本残留端口。",
                installation.Version));
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    private async Task ConnectAndApplyCoreAsync(
        CodexInstallation installation,
        int owner,
        int port,
        string? themeDirectory,
        bool preview,
        CancellationToken cancellationToken)
    {
        await StopWatcherCoreAsync();
        _client = new CdpClient(port);
        var endpoint = await _client.GetEndpointAsync(cancellationToken);
        _browserId = endpoint.BrowserId;
        var payloadDirectory = themeDirectory ?? await _themeCatalog.GetActiveThemeDirectoryAsync(cancellationToken);
        _payload = await _payloadLoader.LoadAsync(payloadDirectory, cancellationToken);
        _isPreviewPayload = preview;
        foreach (var target in await _client.GetAppTargetsAsync(_browserId, cancellationToken)) await AttachTargetAsync(target, cancellationToken);
        if (_sessions.Count == 0) throw new InvalidOperationException("未找到同时具有 Codex 主界面标记的 app:// 页面。");

        Publish(new(
            EngineState.Active,
            preview ? "正在预览草稿" : "主题已应用",
            preview
                ? $"“{_payload.ThemeName}”草稿已临时连接 {_sessions.Count} 个页面；取消预览可恢复当前主题。"
                : $"“{_payload.ThemeName}”已连接 {_sessions.Count} 个页面；页面重载后会自动恢复。",
            installation.Version,
            owner,
            _sessions.Count,
            port));
        _watchCancellation = new CancellationTokenSource();
        _watchTask = WatchTargetsAsync(installation, owner, port, _watchCancellation.Token);
    }

    private async Task AttachTargetAsync(CdpTarget target, CancellationToken cancellationToken)
    {
        if (_sessions.ContainsKey(target.Id) || _payload is null) return;
        var session = new CdpSession();
        try
        {
            await session.OpenAsync(target.WebSocketUrl, cancellationToken);
            var probe = await session.EvaluateAsync("(() => { const root=!!document.getElementById('root'); const shell=!!document.querySelector('main.main-surface, main[data-app-shell-main-surface]'); const content=!!document.querySelector('.composer-surface-chrome, [role=\"main\"], header[data-app-shell-application-menu-bar]'); return { codex: location.protocol === 'app:' && root && shell && content }; })()", cancellationToken);
            if (probe.ValueKind != JsonValueKind.Object || !probe.TryGetProperty("codex", out var codexNode) || !codexNode.GetBoolean())
            {
                await session.DisposeAsync();
                return;
            }
            try
            {
                await session.SendAsync("Runtime.removeBinding", new { name = DreamCommandBinding }, cancellationToken);
            }
            catch
            {
                // Older Chromium builds may not expose removeBinding; addBinding remains safe to try.
            }
            await session.EvaluateAsync("delete globalThis.__dreamSkinCommand", cancellationToken);
            await session.SendAsync("Runtime.addBinding", new { name = DreamCommandBinding }, cancellationToken);
            _earlyScriptIdentifiers[target.Id] = await RegisterEarlyScriptAsync(session, _payload, cancellationToken);
            await session.EvaluateAsync(_payload.Expression, cancellationToken);
            session.EventReceived += async (method, _) =>
            {
                if (method == "Page.loadEventFired" && _payload is not null)
                    try { await session.EvaluateAsync(ThemePayloadLoader.BuildEarlyExpression(_payload), _watchCancellation?.Token ?? CancellationToken.None); } catch { }
            };
            session.EventReceived += async (method, parameters) =>
            {
                if (method != "Runtime.bindingCalled" ||
                    !parameters.TryGetProperty("name", out var nameNode) ||
                    nameNode.GetString() != DreamCommandBinding ||
                    !parameters.TryGetProperty("payload", out var payloadNode))
                {
                    return;
                }

                try
                {
                    await ExecuteDreamCommandAsync(
                        session,
                        payloadNode.GetString(),
                        Snapshot.ListenerProcessId,
                        CancellationToken.None);
                }
                catch
                {
                    // The bridge is deliberately best-effort; native Codex shortcuts remain available.
                }
            };
            _sessions[target.Id] = session;
        }
        catch
        {
            _earlyScriptIdentifiers.Remove(target.Id);
            await session.DisposeAsync();
            throw;
        }
    }

    private static async Task<ThemeRegionMetrics?> MeasureRegionsAsync(CdpSession session, CancellationToken cancellationToken)
    {
        const string expression = """
            (() => {
              const size = element => {
                if (!element || typeof element.getBoundingClientRect !== 'function') return null;
                const box = element.getBoundingClientRect();
                return box.width > 0 && box.height > 0 ? { width: box.width, height: box.height } : null;
              };
              const contentSize = element => element && element.clientWidth > 0 && element.clientHeight > 0
                ? { width: element.clientWidth, height: element.clientHeight } : null;
              const shell = document.querySelector('main.main-surface, main[data-app-shell-main-surface]');
              const sidebar = document.querySelector('aside.app-shell-left-panel');
              if (location.protocol !== 'app:' || !shell) return { codex: false };
              const composer = shell.querySelector('.composer-surface-chrome');
              const home = document.querySelector('[role="main"]:has([data-testid="home-icon"])');
              const homeFrame = home?.querySelector(':scope > div:first-child > div:first-child > div:first-child') || home;
              return {
                codex: true,
                viewport: { width: innerWidth, height: innerHeight },
                background: size(shell),
                sidebar: size(sidebar),
                composer: size(home ? null : composer),
                home: size(homeFrame),
                homeComposer: size(home ? composer : null),
                polaroid: contentSize(home ? document.querySelector('#codex-dream-skin-chrome .dream-polaroid') : null)
              };
            })()
            """;
        var value = await session.EvaluateAsync(expression, cancellationToken);
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("codex", out var codex) || !codex.GetBoolean())
            return null;
        var viewport = ReadRegionSize(value, "viewport");
        var background = ReadRegionSize(value, "background");
        var sidebar = ReadRegionSize(value, "sidebar");
        if (viewport is null || background is null || sidebar is null) return null;
        return new(viewport, background, sidebar, ReadRegionSize(value, "composer"), ReadRegionSize(value, "home"),
            ReadRegionSize(value, "homeComposer"), ReadRegionSize(value, "polaroid"));
    }

    private static ThemeRegionSize? ReadRegionSize(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var node) || node.ValueKind != JsonValueKind.Object ||
            !node.TryGetProperty("width", out var widthNode) || !node.TryGetProperty("height", out var heightNode) ||
            !widthNode.TryGetDouble(out var width) || !heightNode.TryGetDouble(out var height) || width <= 0 || height <= 0)
            return null;
        return new(width, height);
    }

    private static async Task ExecuteDreamCommandAsync(
        CdpSession session,
        string? payload,
        int? processId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 256)
        {
            return;
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("command", out var commandNode))
        {
            return;
        }

        var command = commandNode.GetString();
        if (command == "togglePinnedSummary")
        {
            await session.EvaluateAsync(
                """
                (() => {
                  const button = [...document.querySelectorAll("button")].find((candidate) => {
                    const label = candidate.getAttribute("aria-label") || "";
                    return label.includes("置顶摘要") || /pinned summary/i.test(label);
                  });
                  button?.click();
                  return Boolean(button);
                })()
                """,
                cancellationToken);
            return;
        }

        if (NativeMenuCommands.TryGetValue(command ?? string.Empty, out var nativeCommand))
        {
            if (processId is int trustedProcessId)
            {
                await InvokeNativeMenuCommandAsync(
                    session,
                    trustedProcessId,
                    nativeCommand,
                    cancellationToken);
            }
            return;
        }

        if (!DreamShortcuts.TryGetValue(command ?? string.Empty, out var shortcut))
        {
            return;
        }

        var parameters = new
        {
            modifiers = shortcut.Modifiers,
            key = shortcut.Key,
            code = shortcut.Code,
            windowsVirtualKeyCode = shortcut.VirtualKey,
            nativeVirtualKeyCode = shortcut.VirtualKey
        };
        await session.SendAsync("Input.dispatchKeyEvent", new
        {
            type = "rawKeyDown",
            parameters.modifiers,
            parameters.key,
            parameters.code,
            parameters.windowsVirtualKeyCode,
            parameters.nativeVirtualKeyCode
        }, cancellationToken);
        await session.SendAsync("Input.dispatchKeyEvent", new
        {
            type = "keyUp",
            parameters.modifiers,
            parameters.key,
            parameters.code,
            parameters.windowsVirtualKeyCode,
            parameters.nativeVirtualKeyCode
        }, cancellationToken);
    }

    private static async Task InvokeNativeMenuCommandAsync(
        CdpSession session,
        int processId,
        NativeMenuCommand command,
        CancellationToken cancellationToken)
    {
        var x = command.MenuId switch
        {
            "file" => 102,
            "edit" => 154,
            "help" => 258,
            _ => 206
        };
        await session.EvaluateAsync(
            $"void window.electronBridge?.showApplicationMenu?.({JsonSerializer.Serialize(command.MenuId)}, {x}, 32)",
            cancellationToken);

        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        var itemCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem);
        var condition = new AndCondition(processCondition, itemCondition);
        for (var attempt = 0; attempt < 15; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(40, cancellationToken);
            var items = AutomationElement.RootElement.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement item in items)
            {
                var name = item.Current.Name;
                if (!command.Labels.Any(label =>
                    name.StartsWith(label, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (item.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern) &&
                    pattern is InvokePattern invokePattern)
                {
                    invokePattern.Invoke();
                    return;
                }
            }
        }

        throw new InvalidOperationException($"Codex native menu item was not available: {command.CommandId}");
    }

    private static readonly IReadOnlyDictionary<string, DreamShortcut> DreamShortcuts =
        new Dictionary<string, DreamShortcut>(StringComparer.Ordinal)
        {
            ["newWindow"] = new("N", "KeyN", 78, 10),
            ["newChat"] = new("n", "KeyN", 78, 2),
            ["openFolder"] = new("o", "KeyO", 79, 2),
            ["close"] = new("w", "KeyW", 87, 2),
            ["exit"] = new("q", "KeyQ", 81, 2),
            ["undo"] = new("z", "KeyZ", 90, 2),
            ["redo"] = new("y", "KeyY", 89, 2),
            ["cut"] = new("x", "KeyX", 88, 2),
            ["copy"] = new("c", "KeyC", 67, 2),
            ["paste"] = new("v", "KeyV", 86, 2),
            ["delete"] = new("Delete", "Delete", 46, 0),
            ["selectAll"] = new("a", "KeyA", 65, 2),
            ["settings"] = new(",", "Comma", 188, 2),
            ["toggleSidebar"] = new("b", "KeyB", 66, 2),
            ["toggleBottomPanel"] = new("j", "KeyJ", 74, 2),
            ["openTerminal"] = new("`", "Backquote", 192, 2),
            ["toggleFileTree"] = new("E", "KeyE", 69, 10),
            ["toggleReviewPanel"] = new("b", "KeyB", 66, 3),
            ["openBrowserTab"] = new("t", "KeyT", 84, 2),
            ["focusBrowserAddressBar"] = new("l", "KeyL", 76, 2),
            ["reloadBrowserPage"] = new("r", "KeyR", 82, 2),
            ["find"] = new("f", "KeyF", 70, 2),
            ["previousChat"] = new("{", "BracketLeft", 219, 10),
            ["nextChat"] = new("}", "BracketRight", 221, 10),
            ["back"] = new("[", "BracketLeft", 219, 2),
            ["forward"] = new("]", "BracketRight", 221, 2),
            ["zoomIn"] = new("=", "Equal", 187, 2),
            ["zoomOut"] = new("-", "Minus", 189, 2),
            ["actualSize"] = new("0", "Digit0", 48, 2),
            ["toggleFullScreen"] = new("F11", "F11", 122, 0),
            ["keyboardShortcuts"] = new("/", "Slash", 191, 2)
        };

    private static readonly IReadOnlyDictionary<string, NativeMenuCommand> NativeMenuCommands =
        new Dictionary<string, NativeMenuCommand>(StringComparer.Ordinal)
        {
            ["logout"] = new("logout", "file-menu", ["Log Out", "退出登录"]),
            ["documentation"] = new("documentation", "help-menu", ["Documentation", "使用文档"]),
            ["whatsNew"] = new("whatsNew", "help-menu", ["What's New", "新增功能"]),
            ["troubleshooting"] = new("troubleshooting", "help-menu", ["Troubleshooting", "故障排除"]),
            ["systemStatus"] = new("systemStatus", "help-menu", ["System Status", "系统状态"]),
            ["sendFeedback"] = new("sendFeedback", "help-menu", ["Send Feedback", "发送反馈"]),
            ["startPerformanceTrace"] = new("startPerformanceTrace", "help-menu", ["Start Performance Trace", "开始性能跟踪"]),
            ["about"] = new("about", "help-menu", ["About ChatGPT", "关于 ChatGPT"])
        };

    private sealed record DreamShortcut(
        string Key,
        string Code,
        int VirtualKey,
        int Modifiers);

    private sealed record NativeMenuCommand(
        string CommandId,
        string MenuId,
        IReadOnlyList<string> Labels);

    private async Task WatchTargetsAsync(CodexInstallation installation, int owner, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_listenerVerifier.GetTrustedLoopbackOwner(port, installation) != owner || _client is null || _browserId is null)
                    throw new InvalidOperationException("可信 Codex 监听器已退出或被替换。");
                foreach (var closed in _sessions.Where(item => item.Value.IsClosed).Select(item => item.Key).ToArray())
                {
                    await _sessions[closed].DisposeAsync();
                    _sessions.Remove(closed);
                    _earlyScriptIdentifiers.Remove(closed);
                }
                foreach (var target in await _client.GetAppTargetsAsync(_browserId, cancellationToken))
                    if (!_sessions.ContainsKey(target.Id)) try { await AttachTargetAsync(target, cancellationToken); } catch { }
                Publish(Snapshot with
                {
                    Detail = _isPreviewPayload
                        ? $"“{_payload?.ThemeName}”草稿正在 {_sessions.Count} 个页面中临时预览。"
                        : $"“{_payload?.ThemeName}”已连接 {_sessions.Count} 个页面；页面重载后会自动恢复。",
                    TargetCount = _sessions.Count
                });
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

    private static async Task<string> RegisterEarlyScriptAsync(CdpSession session, ThemePayload payload, CancellationToken cancellationToken)
    {
        var response = await session.SendAsync("Page.addScriptToEvaluateOnNewDocument", new
        {
            source = ThemePayloadLoader.BuildEarlyExpression(payload)
        }, cancellationToken);
        return response.TryGetProperty("identifier", out var identifier) && !string.IsNullOrWhiteSpace(identifier.GetString())
            ? identifier.GetString()!
            : throw new InvalidOperationException("CDP 未返回页面早期脚本标识。");
    }

    private static async Task RemoveEarlyScriptAsync(CdpSession session, string identifier, CancellationToken cancellationToken)
    {
        try { await session.SendAsync("Page.removeScriptToEvaluateOnNewDocument", new { identifier }, cancellationToken); }
        catch when (cancellationToken == CancellationToken.None) { }
    }

    public (int Port, int ProcessId)? FindTrustedListener(CodexInstallation installation)
    {
        for (var port = DefaultPort; port <= LastManagedPort; port++)
        {
            var owner = _listenerVerifier.GetTrustedLoopbackOwner(port, installation);
            if (owner is not null) return (port, owner.Value);
        }
        return null;
    }

    private int? FindAvailablePort()
    {
        for (var port = DefaultPort; port <= LastManagedPort; port++)
            if (!_listenerVerifier.IsOccupied(port)) return port;
        return null;
    }

    private async Task StopWatcherCoreAsync()
    {
        _watchCancellation?.Cancel();
        if (_watchTask is not null) try { await _watchTask; } catch { }
        foreach (var (targetId, session) in _sessions)
        {
            if (_earlyScriptIdentifiers.TryGetValue(targetId, out var identifier))
                await RemoveEarlyScriptAsync(session, identifier, CancellationToken.None);
            await session.DisposeAsync();
        }
        _sessions.Clear();
        _earlyScriptIdentifiers.Clear();
        _client?.Dispose();
        _client = null;
        _browserId = null;
        _isPreviewPayload = false;
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
