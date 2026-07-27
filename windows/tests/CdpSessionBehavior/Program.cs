using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CodexDreamSkin.Services;

await RunAsync();

static async Task RunAsync()
{
    await ScreenshotCaptureDecodesPngAndRemovesPendingRequestAsync();
    await CancellationWhileWaitingForSendGateRemovesPendingRequestAsync();
    await DisconnectWithInflightRequestRemovesPendingRequestAsync();
    Console.WriteLine("PASS: CdpSession cancellation and disconnect cleanup behavior.");
}

static async Task ScreenshotCaptureDecodesPngAndRemovesPendingRequestAsync()
{
    await using var server = await CdpTestServer.StartAsync(disconnectOnApplicationRequest: false);
    await using var session = new CdpSession();
    await session.OpenAsync(server.Endpoint, CancellationToken.None);

    var expected = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2mQAAAABJRU5ErkJggg==");
    var actual = await session.CaptureScreenshotAsync(CancellationToken.None);
    Assert(actual.SequenceEqual(expected), "The screenshot PNG payload was not decoded exactly.");
    Assert(PendingCount(session) == 0, "A completed screenshot request remained in CdpSession._pending.");
}

static async Task CancellationWhileWaitingForSendGateRemovesPendingRequestAsync()
{
    await using var server = await CdpTestServer.StartAsync(disconnectOnApplicationRequest: false);
    await using var session = new CdpSession();
    await session.OpenAsync(server.Endpoint, CancellationToken.None);

    var sendGate = GetPrivateField<SemaphoreSlim>(session, "_sendGate");
    await sendGate.WaitAsync();
    try
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => session.SendAsync("Runtime.evaluate", new { expression = "1" }, cancellation.Token),
            "A request canceled while waiting for the send gate did not report cancellation.");
        Assert(PendingCount(session) == 0,
            "A request canceled while waiting for the send gate remained in CdpSession._pending.");
    }
    finally
    {
        sendGate.Release();
    }
}

static async Task DisconnectWithInflightRequestRemovesPendingRequestAsync()
{
    await using var server = await CdpTestServer.StartAsync(disconnectOnApplicationRequest: true);
    await using var session = new CdpSession();
    await session.OpenAsync(server.Endpoint, CancellationToken.None);

    await AssertThrowsAsync<Exception>(
        () => session.SendAsync("Runtime.evaluate", new { expression = "1" }, CancellationToken.None),
        "A request did not fail after the remote CDP endpoint disconnected.");
    await WaitUntilAsync(() => PendingCount(session) == 0, TimeSpan.FromSeconds(2));
    Assert(PendingCount(session) == 0,
        "An in-flight request remained in CdpSession._pending after disconnect.");
}

static T GetPrivateField<T>(object instance, string name) where T : class =>
    typeof(CdpSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as T
    ?? throw new InvalidOperationException($"Missing private field: {name}");

static int PendingCount(CdpSession session) =>
    GetPrivateField<IDictionary>(session, "_pending").Count;

static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
{
    var deadline = DateTimeOffset.UtcNow + timeout;
    while (!condition() && DateTimeOffset.UtcNow < deadline)
    {
        await Task.Delay(20);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class CdpTestServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly bool _disconnectOnApplicationRequest;
    private readonly Task _serveTask;

    private CdpTestServer(HttpListener listener, int port, bool disconnectOnApplicationRequest)
    {
        _listener = listener;
        _disconnectOnApplicationRequest = disconnectOnApplicationRequest;
        Endpoint = new Uri($"ws://127.0.0.1:{port}/devtools/page/test");
        _serveTask = ServeAsync();
    }

    public Uri Endpoint { get; }

    public static Task<CdpTestServer> StartAsync(bool disconnectOnApplicationRequest)
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return Task.FromResult(new CdpTestServer(listener, port, disconnectOnApplicationRequest));
    }

    private async Task ServeAsync()
    {
        try
        {
            var context = await _listener.GetContextAsync().WaitAsync(_lifetime.Token);
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            using var socket = webSocketContext.WebSocket;
            var buffer = new byte[4096];
            var requestCount = 0;

            while (!_lifetime.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _lifetime.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                requestCount++;
                using var document = JsonDocument.Parse(message.ToArray());
                var id = document.RootElement.GetProperty("id").GetInt32();
                var method = document.RootElement.GetProperty("method").GetString();
                if (_disconnectOnApplicationRequest && requestCount > 2)
                {
                    socket.Abort();
                    return;
                }

                object responseResult = method == "Page.captureScreenshot"
                    ? new
                    {
                        data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2mQAAAABJRU5ErkJggg=="
                    }
                    : new { };
                var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, result = responseResult }));
                await socket.SendAsync(
                    new ArraySegment<byte>(response),
                    WebSocketMessageType.Text,
                    true,
                    _lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (HttpListenerException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _listener.Close();
        try
        {
            await _serveTask;
        }
        catch (Exception) when (_lifetime.IsCancellationRequested)
        {
        }
        _lifetime.Dispose();
    }
}
