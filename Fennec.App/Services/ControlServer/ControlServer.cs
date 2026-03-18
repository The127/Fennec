using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ControlServer;

public class ControlServer : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ControlServer> _logger;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _port;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ControlServer(IServiceProvider services, ILogger<ControlServer> logger)
    {
        _services = services;
        _logger = logger;
        _listener = new HttpListener();

        _port = Environment.GetEnvironmentVariable("FENNEC_CONTROL_PORT") is { } p
            ? int.Parse(p)
            : 8310;
        _listener.Prefixes.Add($"http://*:{_port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            _logger.LogInformation("Control server listening on http://*:{Port}/", _port);
            Task.Run(() => ListenLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start control server");
        }
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting request");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        response.ContentType = "application/json";

        try
        {
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (path == "") path = "/";
            var method = request.HttpMethod;

            var (status, body) = await RouteAsync(method, path, request);
            response.StatusCode = status;
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, JsonOptions));
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {Method} {Path}", request.HttpMethod, request.Url?.AbsolutePath);
            response.StatusCode = 500;
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions));
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
        }
        finally
        {
            response.Close();
        }
    }

    private async Task<(int Status, object Body)> RouteAsync(string method, string path, HttpListenerRequest request)
    {
        // Health
        if (method == "GET" && path == "/health")
            return (200, new { ok = true });

        // Version
        if (method == "GET" && path == "/version")
        {
            var v = Environment.GetEnvironmentVariable("FENNEC_GIT_COMMIT")
                ?? Assembly.GetEntryAssembly()
                    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion
                ?? "unknown";
            return (200, new { version = v });
        }

        // Auth
        if (method == "GET" && path == "/auth/state")
            return await HandleGetAuthStateAsync();
        if (method == "POST" && path == "/auth/login")
            return await HandleLoginAsync(request);
        if (method == "POST" && path == "/auth/logout")
            return await HandleLogoutAsync();

        // Voice
        if (method == "GET" && path == "/voice/state")
            return HandleGetVoiceState();
        if (method == "POST" && path == "/voice/join")
            return await HandleVoiceJoinAsync(request);
        if (method == "POST" && path == "/voice/leave")
            return await HandleVoiceLeaveAsync();
        if (method == "POST" && path == "/voice/mute")
            return await HandleVoiceMuteAsync(request);
        if (method == "POST" && path == "/voice/deafen")
            return await HandleVoiceDeafenAsync(request);

        // Screen share
        if (method == "GET" && path == "/screen-share/targets")
            return await HandleGetScreenShareTargetsAsync();
        if (method == "POST" && path == "/screen-share/start")
            return await HandleScreenShareStartAsync(request);
        if (method == "POST" && path == "/screen-share/stop")
            return await HandleScreenShareStopAsync();
        if (method == "POST" && path.StartsWith("/screen-share/watch/"))
            return await HandleScreenShareWatchAsync(path);
        if (method == "POST" && path.StartsWith("/screen-share/unwatch/"))
            return await HandleScreenShareUnwatchAsync(path);
        if (method == "POST" && path == "/screen-share/update")
            return await HandleScreenShareUpdateAsync(request);
        if (method == "POST" && path == "/screen-share/change-target")
            return await HandleScreenShareChangeTargetAsync(request);
        if (method == "GET" && path.StartsWith("/screen-share/metrics/"))
            return HandleGetScreenShareMetrics(path);
        if (method == "GET" && path.StartsWith("/screen-share/receiving/"))
            return HandleGetScreenShareReceiving(path);

        // Navigation
        if (method == "POST" && path.StartsWith("/navigate/server/"))
            return HandleNavigateToServer(path);
        if (method == "POST" && path.StartsWith("/navigate/channel/"))
            return HandleNavigateToChannel(path);

        // Combined state
        if (method == "GET" && path == "/state")
            return await HandleGetCombinedStateAsync();

        return (404, new { error = "Not found" });
    }

    // --- Auth ---

    private async Task<(int, object)> HandleGetAuthStateAsync()
    {
        var authStore = _services.GetRequiredService<IAuthStore>();
        var session = await authStore.GetCurrentAuthSessionAsync();
        if (session is null)
            return (200, new { isLoggedIn = false });

        return (200, new
        {
            isLoggedIn = true,
            userId = session.UserId,
            username = session.Username,
            instanceUrl = session.Url,
        });
    }

    private async Task<(int, object)> HandleLoginAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsync<LoginRequest>(request);
        if (body is null || string.IsNullOrEmpty(body.Username) || string.IsNullOrEmpty(body.Password) || string.IsNullOrEmpty(body.InstanceUrl))
            return (400, new { error = "username, password, and instanceUrl are required" });

        var authService = _services.GetRequiredService<IAuthService>();
        var session = await authService.LoginAsync(body.Username, body.Password, body.InstanceUrl, CancellationToken.None);
        if (session is null)
            return (401, new { error = "Login failed" });

        // Trigger the full login flow on the UI thread (creates MainAppViewModel, etc.)
        var messenger = _services.GetRequiredService<IMessenger>();
        await Dispatcher.UIThread.InvokeAsync(() => messenger.Send(new LoginSucceededMessage(session)));

        // Give the UI a moment to initialize (load servers, connect hub, etc.)
        await Task.Delay(2000);

        return (200, new
        {
            userId = session.UserId,
            username = session.Username,
            instanceUrl = session.Url,
        });
    }

    private async Task<(int, object)> HandleLogoutAsync()
    {
        var authService = _services.GetRequiredService<IAuthService>();
        await authService.LogoutAsync(CancellationToken.None);
        var messenger = _services.GetRequiredService<IMessenger>();
        await Dispatcher.UIThread.InvokeAsync(() => messenger.Send(new UserLoggedOutMessage()));
        return (200, new { ok = true });
    }

    // --- Voice ---

    private (int, object) HandleGetVoiceState()
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        return (200, new
        {
            isConnected = voice.IsConnected,
            serverId = voice.CurrentServerId,
            channelId = voice.CurrentChannelId,
            isMuted = voice.IsMuted,
            isDeafened = voice.IsDeafened,
            isScreenSharing = voice.IsScreenSharing,
            activeScreenSharers = voice.ActiveScreenSharers.Select(s => new
            {
                userId = s.UserId,
                username = s.Username,
                instanceUrl = s.InstanceUrl,
            }).ToList(),
            peerStates = voice.PeerStates.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
        });
    }

    private async Task<(int, object)> HandleVoiceJoinAsync(HttpListenerRequest request)
    {
        var body = await ReadBodyAsync<VoiceJoinRequest>(request);
        if (body is null || body.ServerId == Guid.Empty || body.ChannelId == Guid.Empty)
            return (400, new { error = "serverId and channelId are required" });

        var authStore = _services.GetRequiredService<IAuthStore>();
        var session = await authStore.GetCurrentAuthSessionAsync();
        if (session is null)
            return (401, new { error = "Not logged in" });

        var instanceUrl = body.InstanceUrl ?? session.Url;

        // Navigate to the server first so ServerViewModel is created
        var messenger = _services.GetRequiredService<IMessenger>();
        await Dispatcher.UIThread.InvokeAsync(() =>
            messenger.Send(new ControlNavigateToServerMessage(body.ServerId)));

        // Wait for navigation to complete
        await Task.Delay(1000);

        var voice = _services.GetRequiredService<IVoiceCallService>();
        await voice.JoinAsync(body.ServerId, body.ChannelId, instanceUrl, session.UserId, session.Username);

        return (200, new { ok = true });
    }

    private async Task<(int, object)> HandleVoiceLeaveAsync()
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        await voice.LeaveAsync();
        return (200, new { ok = true });
    }

    private async Task<(int, object)> HandleVoiceMuteAsync(HttpListenerRequest request)
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        if (!voice.IsConnected)
            return (400, new { error = "Not in a voice channel" });

        var body = await ReadBodyAsync<MuteRequest>(request);
        var muted = body?.Muted ?? !voice.IsMuted;
        voice.SetMuted(muted);
        return (200, new { isMuted = voice.IsMuted });
    }

    private async Task<(int, object)> HandleVoiceDeafenAsync(HttpListenerRequest request)
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        if (!voice.IsConnected)
            return (400, new { error = "Not in a voice channel" });

        var body = await ReadBodyAsync<DeafenRequest>(request);
        var deafened = body?.Deafened ?? !voice.IsDeafened;
        voice.SetDeafened(deafened);
        return (200, new { isDeafened = voice.IsDeafened, isMuted = voice.IsMuted });
    }

    // --- Screen Share ---

    private async Task<(int, object)> HandleGetScreenShareTargetsAsync()
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        var targets = await voice.GetScreenShareTargetsAsync();
        return (200, new
        {
            targets = targets.Select(t => new
            {
                kind = t.Kind.ToString().ToLowerInvariant(),
                id = t.Id,
                name = t.Name,
                width = t.Width,
                height = t.Height,
            }).ToList(),
        });
    }

    private async Task<(int, object)> HandleScreenShareStartAsync(HttpListenerRequest request)
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        if (!voice.IsConnected)
            return (400, new { error = "Not in a voice channel" });

        var body = await ReadBodyAsync<ScreenShareStartRequest>(request);
        var resolution = body?.Resolution ?? "1080p";
        var bitrateKbps = body?.BitrateKbps ?? 1500;
        var frameRate = body?.FrameRate ?? 30;

        if (voice.IsScreenSharing)
            return (409, new { error = "Already screen sharing" });

        if (body?.TargetId is not null)
        {
            var targets = await voice.GetScreenShareTargetsAsync();
            var target = targets.FirstOrDefault(t => t.Id == body.TargetId);
            if (target is null)
                return (404, new { error = $"Target '{body.TargetId}' not found" });
            await voice.StartScreenShareAsync(target, resolution, bitrateKbps, frameRate);
        }
        else
        {
            // Default: first screen target
            var targets = await voice.GetScreenShareTargetsAsync();
            var target = targets.FirstOrDefault(t => t.Kind == CaptureTargetKind.Screen)
                         ?? targets.FirstOrDefault();
            if (target is null)
                return (404, new { error = "No capture targets available" });
            await voice.StartScreenShareAsync(target, resolution, bitrateKbps, frameRate);
        }

        if (!voice.IsScreenSharing)
            return (500, new { error = "Screen share failed to start" });

        return (200, new { ok = true, isScreenSharing = true });
    }

    private async Task<(int, object)> HandleScreenShareStopAsync()
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        await voice.StopScreenShareAsync();
        return (200, new { ok = true });
    }

    private async Task<(int, object)> HandleScreenShareUpdateAsync(HttpListenerRequest request)
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        if (!voice.IsScreenSharing)
            return (400, new { error = "Not screen sharing" });

        var body = await ReadBodyAsync<ScreenShareUpdateRequest>(request);
        if (body is null)
            return (400, new { error = "Request body required" });

        var resolution = body.Resolution ?? "1080p";
        var bitrateKbps = body.BitrateKbps ?? 1500;
        var frameRate = body.FrameRate ?? 30;

        await voice.UpdateScreenShareSettingsAsync(resolution, bitrateKbps, frameRate);
        return (200, new { ok = true, resolution, bitrateKbps, frameRate });
    }

    private async Task<(int, object)> HandleScreenShareChangeTargetAsync(HttpListenerRequest request)
    {
        var voice = _services.GetRequiredService<IVoiceCallService>();
        if (!voice.IsScreenSharing)
            return (400, new { error = "Not screen sharing" });

        var body = await ReadBodyAsync<ScreenShareStartRequest>(request);
        var resolution = body?.Resolution ?? "1080p";
        var bitrateKbps = body?.BitrateKbps ?? 1500;
        var frameRate = body?.FrameRate ?? 30;

        // Stop current share and start with new target
        await voice.StopScreenShareAsync();
        // Brief delay to let cleanup complete
        await Task.Delay(500);

        if (body?.TargetId is not null)
        {
            var targets = await voice.GetScreenShareTargetsAsync();
            var target = targets.FirstOrDefault(t => t.Id == body.TargetId);
            if (target is null)
                return (404, new { error = $"Target '{body.TargetId}' not found" });
            await voice.StartScreenShareAsync(target, resolution, bitrateKbps, frameRate);
        }
        else
        {
            var targets = await voice.GetScreenShareTargetsAsync();
            var target = targets.FirstOrDefault(t => t.Kind == CaptureTargetKind.Screen)
                         ?? targets.FirstOrDefault();
            if (target is null)
                return (404, new { error = "No capture targets available" });
            await voice.StartScreenShareAsync(target, resolution, bitrateKbps, frameRate);
        }

        return (200, new { ok = true });
    }

    private async Task<(int, object)> HandleScreenShareWatchAsync(string path)
    {
        // /screen-share/watch/{userId}
        var segment = path["/screen-share/watch/".Length..];
        if (!Guid.TryParse(segment, out var userId))
            return (400, new { error = "Invalid userId" });

        var voice = _services.GetRequiredService<IVoiceCallService>();
        if (!voice.IsConnected)
            return (400, new { error = "Not in a voice channel" });

        // Call VoiceCallService directly to avoid depending on ViewModel state
        await voice.WatchScreenShareAsync(userId);

        // Also notify the UI so the ViewModel updates its watched shares list
        var messenger = _services.GetRequiredService<IMessenger>();
        Dispatcher.UIThread.Post(() => messenger.Send(new ControlWatchScreenShareMessage(userId)));
        return (200, new { ok = true });
    }

    private async Task<(int, object)> HandleScreenShareUnwatchAsync(string path)
    {
        // /screen-share/unwatch/{userId}
        var segment = path["/screen-share/unwatch/".Length..];
        if (!Guid.TryParse(segment, out var userId))
            return (400, new { error = "Invalid userId" });

        var voice = _services.GetRequiredService<IVoiceCallService>();
        if (!voice.IsConnected)
            return (400, new { error = "Not in a voice channel" });

        await voice.UnwatchScreenShareAsync(userId);

        var messenger = _services.GetRequiredService<IMessenger>();
        Dispatcher.UIThread.Post(() => messenger.Send(new ControlUnwatchScreenShareMessage(userId)));
        return (200, new { ok = true });
    }

    private (int, object) HandleGetScreenShareMetrics(string path)
    {
        // /screen-share/metrics/{userId}
        var segment = path["/screen-share/metrics/".Length..];
        if (!Guid.TryParse(segment, out var userId))
            return (400, new { error = "Invalid userId" });

        var voice = _services.GetRequiredService<IVoiceCallService>();
        var m = voice.GetMetrics(userId);
        return (200, new
        {
            isSender = m.IsSender,
            // Sender metrics
            captureFps = m.CaptureFps.Latest,
            encodeTimeMs = m.EncodeTimeMs.Latest,
            encodedSizeKb = m.EncodedSizeKb.Latest,
            sentFps = m.SentFps.Latest,
            captureWidth = m.CaptureWidth,
            captureHeight = m.CaptureHeight,
            framesEncoded = m.FramesEncoded,
            framesSent = m.FramesSent,
            framesDropped = m.FramesDropped,
            encoderName = m.EncoderName,
            viewerCount = m.ViewerCount,
            // Receiver metrics
            transportFps = m.TransportFps.Latest,
            receiveFps = m.ReceiveFps.Latest,
            decodeTimeMs = m.DecodeTimeMs.Latest,
            framesReceived = m.FramesReceived,
            framesDecoded = m.FramesDecoded,
            // UI metrics
            renderFps = m.RenderFps.Latest,
            queueDepth = m.QueueDepth.Latest,
            frameLagMs = m.FrameLagMs.Latest,
        });
    }

    private (int, object) HandleGetScreenShareReceiving(string path)
    {
        // /screen-share/receiving/{userId}
        var segment = path["/screen-share/receiving/".Length..];
        if (!Guid.TryParse(segment, out var userId))
            return (400, new { error = "Invalid userId" });

        var voice = _services.GetRequiredService<IVoiceCallService>();
        var m = voice.GetMetrics(userId);
        var isReceiving = m.FramesReceived > 0 && m.ReceiveFps.Latest > 0;
        return (200, new
        {
            isReceiving,
            framesReceived = m.FramesReceived,
            receiveFps = m.ReceiveFps.Latest,
        });
    }

    // --- Navigation ---

    private (int, object) HandleNavigateToServer(string path)
    {
        // /navigate/server/{serverId}
        var segment = path["/navigate/server/".Length..];
        if (!Guid.TryParse(segment, out var serverId))
            return (400, new { error = "Invalid serverId" });

        var messenger = _services.GetRequiredService<IMessenger>();
        Dispatcher.UIThread.Post(() => messenger.Send(new ControlNavigateToServerMessage(serverId)));
        return (200, new { ok = true });
    }

    private (int, object) HandleNavigateToChannel(string path)
    {
        // /navigate/channel/{serverId}/{channelId}
        var rest = path["/navigate/channel/".Length..];
        var parts = rest.Split('/');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var serverId) || !Guid.TryParse(parts[1], out _))
            return (400, new { error = "Invalid serverId/channelId" });

        // Navigate to the server (channel selection within server view isn't directly routable)
        var messenger = _services.GetRequiredService<IMessenger>();
        Dispatcher.UIThread.Post(() => messenger.Send(new ControlNavigateToServerMessage(serverId)));
        return (200, new { ok = true });
    }

    // --- Combined State ---

    private async Task<(int, object)> HandleGetCombinedStateAsync()
    {
        var (_, authState) = await HandleGetAuthStateAsync();
        var (_, voiceState) = HandleGetVoiceState();
        return (200, new { auth = authState, voice = voiceState });
    }

    // --- Helpers ---

    private static async Task<T?> ReadBodyAsync<T>(HttpListenerRequest request) where T : class
    {
        if (!request.HasEntityBody)
            return null;
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
    }
}

// --- Request DTOs ---

file record LoginRequest
{
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? InstanceUrl { get; init; }
}

file record VoiceJoinRequest
{
    public Guid ServerId { get; init; }
    public Guid ChannelId { get; init; }
    public string? InstanceUrl { get; init; }
}

file record MuteRequest
{
    public bool? Muted { get; init; }
}

file record DeafenRequest
{
    public bool? Deafened { get; init; }
}

file record ScreenShareStartRequest
{
    public string? TargetId { get; init; }
    public string? Resolution { get; init; }
    public int? BitrateKbps { get; init; }
    public int? FrameRate { get; init; }
}

file record ScreenShareUpdateRequest
{
    public string? Resolution { get; init; }
    public int? BitrateKbps { get; init; }
    public int? FrameRate { get; init; }
}
