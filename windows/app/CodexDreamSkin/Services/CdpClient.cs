using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace CodexDreamSkin.Services;

public sealed record CdpEndpoint(string BrowserId, Uri BrowserWebSocketUrl);

public sealed record CdpTarget(string Id, string Type, string Url, Uri WebSocketUrl);

public sealed class CdpClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public CdpClient(int port)
    {
        Port = port;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
            Timeout = TimeSpan.FromSeconds(2)
        };
    }

    public int Port { get; }

    public async Task<CdpEndpoint> GetEndpointAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("json/version", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var rawUrl = document.RootElement.GetProperty("webSocketDebuggerUrl").GetString()
            ?? throw new InvalidDataException("CDP version response did not contain a WebSocket URL.");
        var uri = ValidateWebSocketUri(rawUrl, Port, "browser", out var browserId);
        return new CdpEndpoint(browserId, uri);
    }

    public async Task<IReadOnlyList<CdpTarget>> GetAppTargetsAsync(string expectedBrowserId, CancellationToken cancellationToken)
    {
        var endpoint = await GetEndpointAsync(cancellationToken);
        if (!string.Equals(endpoint.BrowserId, expectedBrowserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CDP browser identity changed; refusing to attach to a replacement listener.");
        }

        using var response = await _httpClient.GetAsync("json/list", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("CDP target list was not an array.");
        }

        var targets = new List<CdpTarget>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idNode) || !item.TryGetProperty("type", out var typeNode) ||
                !item.TryGetProperty("url", out var urlNode) || !item.TryGetProperty("webSocketDebuggerUrl", out var wsNode))
            {
                continue;
            }

            var id = idNode.GetString();
            var type = typeNode.GetString();
            var url = urlNode.GetString();
            var rawWebSocketUrl = wsNode.GetString();
            if (id is null || type != "page" || url is null || !url.StartsWith("app://", StringComparison.Ordinal) || rawWebSocketUrl is null)
            {
                continue;
            }

            try
            {
                var uri = ValidateWebSocketUri(rawWebSocketUrl, Port, "page", out var urlId);
                if (string.Equals(id, urlId, StringComparison.Ordinal))
                {
                    targets.Add(new CdpTarget(id, type, url, uri));
                }
            }
            catch (InvalidDataException)
            {
                // Ignore targets that point outside the trusted loopback endpoint shape.
            }
        }

        return targets;
    }

    internal static Uri ValidateWebSocketUri(string value, int port, string kind, out string identifier)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "ws" ||
            uri.Host != "127.0.0.1" || uri.Port != port || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidDataException("Rejected a CDP WebSocket URL outside the trusted loopback endpoint.");
        }

        var prefix = $"/devtools/{kind}/";
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) || uri.AbsolutePath.Length <= prefix.Length)
        {
            throw new InvalidDataException("Rejected an invalid CDP WebSocket path.");
        }

        identifier = uri.AbsolutePath[prefix.Length..];
        if (identifier.Length > 200 || identifier.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-')))
        {
            throw new InvalidDataException("Rejected an invalid CDP identity.");
        }

        return uri;
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed class CdpSession : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = [];
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private int _nextId;
    private Task? _receiveTask;

    public event Func<string, JsonElement, Task>? EventReceived;

    public bool IsClosed => _socket.State != WebSocketState.Open;

    public async Task OpenAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await _socket.ConnectAsync(endpoint, timeout.Token);
        _receiveTask = ReceiveLoopAsync(_lifetime.Token);
        await SendAsync("Runtime.enable", null, cancellationToken);
        await SendAsync("Page.enable", null, cancellationToken);
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("CDP session is not open.");
        }

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending)
        {
            _pending[id] = completion;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters ?? new { } });
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendGate.Release();
        }

        try
        {
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        finally
        {
            lock (_pending)
            {
                _pending.Remove(id);
            }
        }
    }

    public async Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        var response = await SendAsync("Runtime.evaluate", new
        {
            expression,
            awaitPromise = true,
            returnByValue = true,
            userGesture = false
        }, cancellationToken);

        if (response.TryGetProperty("exceptionDetails", out var exception))
        {
            throw new InvalidOperationException($"Renderer evaluation failed: {exception}");
        }

        return response.TryGetProperty("result", out var result) && result.TryGetProperty("value", out var value)
            ? value.Clone()
            : default;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                using var document = JsonDocument.Parse(message.ToArray());
                message.SetLength(0);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var idNode))
                {
                    TaskCompletionSource<JsonElement>? completion;
                    lock (_pending)
                    {
                        _pending.TryGetValue(idNode.GetInt32(), out completion);
                    }

                    if (completion is not null)
                    {
                        if (root.TryGetProperty("error", out var error))
                        {
                            completion.TrySetException(new InvalidOperationException(error.ToString()));
                        }
                        else
                        {
                            completion.TrySetResult(root.GetProperty("result").Clone());
                        }
                    }
                }
                else if (root.TryGetProperty("method", out var methodNode) && EventReceived is { } handler)
                {
                    var parameters = root.TryGetProperty("params", out var paramsNode) ? paramsNode.Clone() : default;
                    _ = handler(methodNode.GetString() ?? string.Empty, parameters);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            FailPending(error);
        }
        finally
        {
            FailPending(new IOException("CDP socket closed."));
        }
    }

    private void FailPending(Exception error)
    {
        lock (_pending)
        {
            foreach (var completion in _pending.Values)
            {
                completion.TrySetException(error);
            }

            _pending.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Manager closed", CancellationToken.None);
            }
            catch
            {
                _socket.Abort();
            }
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask; } catch { }
        }

        _socket.Dispose();
        _sendGate.Dispose();
        _lifetime.Dispose();
    }
}
