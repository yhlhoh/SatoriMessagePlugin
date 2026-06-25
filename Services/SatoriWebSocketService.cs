using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatoriMessagePlugin.Models;

namespace SatoriMessagePlugin.Services;

[NotificationProviderInfo(
    "3a8f7b6c-d123-4e5f-a6b7-c8d9e0f1a2b3",
    "Satori消息",
    "",
    "连接到 Satori 服务，接收消息并转发为 ClassIsland 提醒。")]
public class SatoriWebSocketService : NotificationProviderBase, IHostedService
{
    private readonly SatoriConnectionSettings _settings;
    private readonly SatoriMessageInfo _latestMessage;
    private readonly ILogger<SatoriWebSocketService> _logger;
    private CancellationTokenSource? _cts;
    private ClientWebSocket? _ws;
    private readonly object _lock = new();
    private string _lastDeduplicationKey = "";
    private bool _isConnected;

    // 连接状态（供设置页绑定）
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected == value) return;
            _isConnected = value;
            OnConnectionStateChanged?.Invoke();
        }
    }

    public event Action? OnConnectionStateChanged;

    public SatoriWebSocketService(
        SatoriConnectionSettings settings,
        SatoriMessageInfo latestMessage,
        ILogger<SatoriWebSocketService> logger)
    {
        _settings = settings;
        _latestMessage = latestMessage;
        _logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SatoriMessagePlugin 服务启动");

        if (_settings.IsEnabled && !string.IsNullOrWhiteSpace(_settings.SatoriWsUrl))
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(() => RunConnectionLoopAsync(_cts.Token), CancellationToken.None);
        }

        // 订阅设置变更以支持运行时启用/禁用
        _settings.PropertyChanged += OnSettingsChanged;

        return Task.CompletedTask;
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SatoriMessagePlugin 服务正在停止...");
        _settings.PropertyChanged -= OnSettingsChanged;

        if (_cts != null)
        {
            try
            {
                await _cts.CancelAsync();
            }
            catch { }
            _cts.Dispose();
            _cts = null;
        }

        await DisconnectAsync();
        _logger.LogInformation("SatoriMessagePlugin 服务已停止");
    }

    private async void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SatoriConnectionSettings.IsEnabled))
        {
            if (_settings.IsEnabled && !string.IsNullOrWhiteSpace(_settings.SatoriWsUrl))
            {
                // 启动连接
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => RunConnectionLoopAsync(_cts.Token), CancellationToken.None);
            }
            else
            {
                // 停止连接
                if (_cts != null)
                {
                    await _cts.CancelAsync();
                    _cts.Dispose();
                    _cts = null;
                }
                await DisconnectAsync();
            }
        }
    }

    private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_settings.SatoriWsUrl))
                {
                    _logger.LogWarning("Satori WebSocket 地址为空，等待配置...");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                await ConnectAndReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebSocket 连接异常");
            }

            if (!_settings.AutoReconnect || cancellationToken.IsCancellationRequested)
                break;

            _logger.LogInformation("将在 {Delay}s 后重连...", _settings.ReconnectDelaySeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.ReconnectDelaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndReceiveAsync(CancellationToken cancellationToken)
    {
        var url = _settings.SatoriWsUrl.Trim();
        _logger.LogInformation("正在连接 Satori WebSocket: {Url}", url);

        lock (_lock)
        {
            _ws?.Dispose();
            _ws = new ClientWebSocket();
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await _ws.ConnectAsync(new Uri(url), linkedCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("连接超时: {Url}", url);
            IsConnected = false;
            return;
        }

        IsConnected = true;
        _logger.LogInformation("已连接到 Satori 服务: {Url}", url);

        try
        {
            await ReceiveLoopAsync(_ws, cancellationToken);
        }
        finally
        {
            IsConnected = false;
            lock (_lock)
            {
                if (_ws?.State == WebSocketState.Open || _ws?.State == WebSocketState.CloseReceived)
                {
                    try
                    {
                        _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(TimeSpan.FromSeconds(3));
                    }
                    catch { }
                }
                _ws?.Dispose();
                _ws = null;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("服务器关闭了 WebSocket 连接");
                break;
            }

            messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (result.EndOfMessage)
            {
                var rawText = messageBuilder.ToString();
                messageBuilder.Clear();
                ProcessRawMessage(rawText);
            }
        }
    }

    private void ProcessRawMessage(string rawText)
    {
        try
        {
            var node = JsonNode.Parse(rawText);
            if (node is not JsonObject obj) return;

            // Satori 推送可能包裹在 { "type": "message", "body": {...} } 中
            // 也可能是直接的事件对象
            var type = obj["type"]?.GetValue<string>();
            JsonObject? body = null;

            if (!string.IsNullOrWhiteSpace(type))
            {
                body = obj["body"]?.AsObject();
            }
            else if (obj["body"] != null)
            {
                // 有些实现把事件包装一层
                body = obj["body"]?.AsObject();
            }

            if (body == null) return;

            // 只处理 message 事件
            if (!string.IsNullOrWhiteSpace(type) && type != "message")
                return;

            ProcessSatoriBody(body);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "JSON 解析失败");
        }
    }

    private void ProcessSatoriBody(JsonObject body)
    {
        // 检查是否有 user 和 content，这是 message 事件的特征
        var hasUser = body["user"] != null;
        var hasContent = body["content"] != null || body["message"]?["content"] != null;

        if (!hasUser && !hasContent) return;

        // 创建临时消息对象进行解析和过滤
        var tempInfo = new SatoriMessageInfo();
        tempInfo.UpdateFromSatoriBody(body);

        if (!tempInfo.HasMessage) return;

        // 黑名单过滤
        if (IsMuted(tempInfo))
        {
            _logger.LogDebug("消息被过滤: Sender={Sender}, Group={Group}",
                tempInfo.Sender, tempInfo.GroupName);
            return;
        }

        // 去重检查
        var dedupKey = tempInfo.GetDeduplicationKey();
        if (dedupKey == _lastDeduplicationKey)
        {
            _logger.LogDebug("消息去重跳过: Key={Key}", dedupKey);
            return;
        }
        _lastDeduplicationKey = dedupKey;

        // 更新最新消息状态（在 UI 线程）
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _latestMessage.UpdateFromSatoriBody(body);
        });

        // 发送通知（在 UI 线程）
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ShowSatoriNotification(tempInfo);
        });

        _logger.LogInformation("收到消息: {Sender}: {Content}",
            tempInfo.NotificationTitle,
            Truncate(tempInfo.NotificationBody, 50));
    }

    private bool IsMuted(SatoriMessageInfo info)
    {
        // 检查发件人黑名单
        if (_settings.MutedSenders.Any(x =>
                info.Sender.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 检查群聊黑名单
        if (!string.IsNullOrWhiteSpace(info.GroupName)
            && _settings.MutedGroups.Any(x =>
                info.GroupName.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private void ShowSatoriNotification(SatoriMessageInfo info)
    {
        try
        {
            var overlay = NotificationContent.CreateSimpleTextContent(
                info.NotificationBody, content =>
                {
                    content.Duration = TimeSpan.FromSeconds(5);
                    content.SpeechContent = info.NotificationBody;
                });

            var request = new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask(
                    info.NotificationTitle,
                    rightIcon: "",
                    factory: content =>
                    {
                        content.Duration = TimeSpan.FromSeconds(2);
                        content.SpeechContent = info.NotificationTitle;
                    }),
                OverlayContent = overlay,
                RequestNotificationSettings =
                {
                    IsSettingsEnabled = true,
                    IsSpeechEnabled = true,
                    IsNotificationEffectEnabled = true,
                    IsNotificationSoundEnabled = true,
                    IsNotificationTopmostEnabled = false
                }
            };

            ShowNotification(request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送通知失败");
        }
    }

    private async Task DisconnectAsync()
    {
        ClientWebSocket? ws;
        lock (_lock)
        {
            ws = _ws;
            _ws = null;
        }

        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", cts.Token);
            }
            catch { }
        }

        try { ws?.Dispose(); } catch { }
        IsConnected = false;
    }

    /// <summary>供设置页手动重连</summary>
    public async Task ReconnectAsync()
    {
        await DisconnectAsync();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunConnectionLoopAsync(_cts.Token), CancellationToken.None);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? "";
        return value[..maxLength] + "...";
    }
}
