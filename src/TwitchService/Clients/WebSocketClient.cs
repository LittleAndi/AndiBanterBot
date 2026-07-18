namespace Application.Features.Twitch;

public interface IWebSocketClient : IDisposable
{
    Task<bool> ConnectAsync(string url, CancellationToken cancellationToken = default);
    Task ReceiveMessagesAsync(CancellationToken cancellationToken = default);
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
    event EventHandler<TwitchMessageEventArgs> OnMessageReceived;
    event EventHandler<TwitchMessageEventArgs> OnWelcomeReceived;
    event EventHandler OnCloseReceived;
}

public class WebSocketClient(ILogger<WebSocketClient> logger) : IWebSocketClient
{
    private ClientWebSocket _webSocket = new();
    private const int ReceiveBufferSize = 8192;
    private bool _disposed;

    public event EventHandler<TwitchMessageEventArgs>? OnMessageReceived;
    public event EventHandler<TwitchMessageEventArgs>? OnWelcomeReceived;
    public event EventHandler? OnCloseReceived;

    public async Task<bool> ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Check if we're already running
        if (_webSocket.State is WebSocketState.Open or WebSocketState.Connecting)
        {
            return false;
        }

        // ClientWebSocket instances are single-use, so any previously used socket must be replaced
        if (_webSocket.State is not WebSocketState.None)
        {
            _webSocket.Dispose();
            _webSocket = new ClientWebSocket();
        }

        logger.LogInformation("Connecting to WebSocket at {Url}", url);
        await _webSocket.ConnectAsync(new Uri(url), cancellationToken);
        logger.LogInformation("WebSocket connected successfully");
        return true;
    }

    public async Task ReceiveMessagesAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferSize];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                logger.LogInformation("Received message of type {MessageType}", result.MessageType);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogInformation("Received WebSocket close message");
                    // await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                    //     string.Empty, _cancellationTokenSource.Token);

                    OnCloseReceived?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var response = JsonSerializer.Deserialize<TwitchResponse>(message);

                    if (response == null)
                    {
                        logger.LogWarning("Received null response from WebSocket");
                        continue;
                    }

                    logger.LogInformation("Received message metadata message type: {MetadataMessageType}", response.Metadata.MessageType);

                    var args = new TwitchMessageEventArgs(response, message);

                    if (response?.Metadata.MessageType == "session_welcome")
                    {
                        logger.LogInformation(
                            "Connected to session {SessionId} with keepalive timeout of {TimeoutSeconds} seconds",
                            response.Payload.Session.Id,
                            response.Payload.Session.KeepaliveTimeoutSeconds);
                        OnWelcomeReceived?.Invoke(this, args);
                    }

                    OnMessageReceived?.Invoke(this, args);
                }
            }

            logger.LogInformation("WebSocket connection closed, loop exiting");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebSocket error occurred");
        }
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_webSocket.State != WebSocketState.Open)
        {
            logger.LogError("Cannot send message - WebSocket is not connected");
            throw new InvalidOperationException("WebSocket is not connected");
        }

        logger.LogInformation("Sending message: {Message}", message);
        var buffer = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(buffer),
            WebSocketMessageType.Text, true, cancellationToken);
    }

    public async void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            logger.LogInformation("Disposing WebSocket client");
            _disposed = true;

            if (_webSocket.State == WebSocketState.Open)
            {
                // Try to close the connection gracefully with a timeout
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "Client shutting down", timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("WebSocket close operation timed out");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during WebSocket shutdown");
                }
            }
        }
        finally
        {
            _webSocket.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebSocketClient));
        }
    }
}

public class TwitchMessageEventArgs(TwitchResponse? response, string rawMessage) : EventArgs
{
    public TwitchResponse? Response { get; } = response;
    public string RawMessage { get; } = rawMessage;
}