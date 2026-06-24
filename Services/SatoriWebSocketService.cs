using System.Net.WebSockets;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared.Models.Notification;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SatoriMessagePlugin.Models;

namespace SatoriMessagePlugin.Services;

/// <summary>
/// Satori WebSocket 服务：连接、过滤、通知、UI 更新
/// </summary>
public class SatoriWebSocketService : IHostedService
{
    private readonly Plugin _plugin;
    private readonly SatoriConnectionSettings _settings;
    private readonly INotificationHostService? _notificationHost;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;

    /// <summary>解析后的屏蔽用户 ID 集合（内存缓存）</summary>
    private HashSet<string> _blockedUserIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>解析后的屏蔽频道 ID 集合（内存缓存）</summary>
    private HashSet<string> _blockedChannelIds = new(StringComparer.OrdinalIgnoreCase);

    public SatoriWebSocketService(
        Plugin plugin,
        SatoriConnectionSettings settings,
        INotificationHostService? notificationHost = null)
    {
        _plugin = plugin;
        _settings = settings;
        _notificationHost = notificationHost;
        RefreshBlocklists();

        // 当设置变更时刷新黑名单缓存
        _settings.PropertyChanged += (_, _) => RefreshBlocklists();
    }

    /// <summary>
    /// 重新解析屏蔽列表
    /// </summary>
    private void RefreshBlocklists()
    {
        _blockedUserIds = ParseIdList(_settings.BlockedUserIds);
        _blockedChannelIds = ParseIdList(_settings.BlockedChannelIds);
    }

    private static HashSet<string> ParseIdList(string raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return set;

        foreach (var line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                set.Add(trimmed);
        }
        return set;
    }

    // ========================================================================
    // IHostedService
    // ========================================================================

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveLoop = ConnectAndReceiveAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_receiveLoop != null)
        {
            try { await _receiveLoop; }
            catch (OperationCanceledException) { }
        }
        await DisconnectAsync();
    }

    // ========================================================================
    // 连接管理
    // ========================================================================

    private async Task ConnectAndReceiveAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(ct);
                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] 连接异常: {ex.Message}");
            }

            if (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(5000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        await DisconnectAsync();
        _webSocket = new ClientWebSocket();

        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_settings.Token}");
        }

        var uri = new Uri(_settings.WebSocketUrl);
        await _webSocket.ConnectAsync(uri, ct);
        System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] 已连接到 {uri}");
    }

    private async Task DisconnectAsync()
    {
        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
                _webSocket.Dispose();
            }
            catch { }
            _webSocket = null;
        }
    }

    // ========================================================================
    // 消息接收 & 处理
    // ========================================================================

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_webSocket == null) return;

        var buffer = new byte[65536];
        var messageBuilder = new StringBuilder();

        while (_webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            messageBuilder.Clear();
            WebSocketReceiveResult result;

            do
            {
                result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "", ct);
                    return;
                }

                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                ProcessMessage(messageBuilder.ToString());
            }
        }
    }

    private void ProcessMessage(string rawJson)
    {
        try
        {
            var json = JObject.Parse(rawJson);
            var eventType = json["type"]?.ToString();

            if (eventType is not ("message-created" or "message-updated"))
                return;

            var channel = json["channel"];
            var user = json["user"];
            var message = json["message"];
            var guild = json["guild"];

            if (message == null || user == null || channel == null) return;

            var senderId = user["id"]?.ToString() ?? "";
            var channelId = channel["id"]?.ToString() ?? "";
            var channelType = channel["type"]?.ToString() ?? "0";

            // ---- 过滤：黑名单 ----
            if (_blockedUserIds.Contains(senderId))
            {
                System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] 已屏蔽用户 {senderId}");
                return;
            }
            if (_blockedChannelIds.Contains(channelId))
            {
                System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] 已屏蔽频道 {channelId}");
                return;
            }

            // ---- 提取字段 ----
            var senderName = user["name"]?.ToString() ?? "";
            var senderNickname = user["nick"]?.ToString() ?? "";
            var content = message["content"]?.ToString() ?? "";
            var messageId = message["id"]?.ToString() ?? "";
            var channelName = channel["name"]?.ToString() ?? "";
            var guildId = guild?["id"]?.ToString() ?? "";
            var guildName = guild?["name"]?.ToString() ?? "";
            var platform = json["platform"]?.ToString() ?? "";

            // 截断过长内容
            if (_settings.MaxContentLength > 0 && content.Length > _settings.MaxContentLength)
            {
                content = content[.._settings.MaxContentLength] + "...";
            }

            var timestamp = DateTime.Now;
            var isPrivate = channelType == "1"; // DIRECT

            // ---- 更新 UI 组件 ----
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var m = _plugin.LatestMessage;
                m.SenderId = senderId;
                m.SenderName = senderName;
                m.SenderNickname = senderNickname;
                m.Content = content;
                m.ChannelId = channelId;
                m.ChannelName = channelName;
                m.ChannelType = channelType;
                m.GuildId = guildId;
                m.GuildName = guildName;
                m.Platform = platform;
                m.Timestamp = timestamp;
                m.MessageId = messageId;
            });

            // ---- 触发 ClassIsland 原生提醒 ----
            if (_settings.EnableNotification)
            {
                TriggerNotification(senderName, senderNickname, senderId,
                                    content, guildName, channelName, isPrivate);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] 解析消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 触发 ClassIsland 原生提醒
    /// 私聊格式: 联系人:消息
    /// 群聊格式: 发件人(群名):消息
    /// </summary>
    private void TriggerNotification(
        string senderName, string senderNickname, string senderId,
        string content, string guildName, string channelName,
        bool isPrivate)
    {
        if (_notificationHost == null) return;

        var displaySender = !string.IsNullOrEmpty(senderNickname) ? senderNickname :
                            !string.IsNullOrEmpty(senderName) ? senderName :
                            senderId;

        var displayGroup = !string.IsNullOrEmpty(guildName) ? guildName : channelName;

        string maskText;
        string overlayText;

        if (isPrivate)
        {
            maskText = displaySender;
            overlayText = content;
        }
        else
        {
            maskText = $"{displaySender}({displayGroup})";
            overlayText = content;
        }

        Application.Current?.Dispatcher.Invoke(() =>
        {
            _notificationHost.ShowNotification(new NotificationRequest()
            {
                MaskContent = new TextBlock(new Run(maskText))
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 24,
                    FontWeight = FontWeights.SemiBold
                },
                OverlayContent = new TextBlock(new Run(overlayText))
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 16,
                    TextWrapping = TextWrapping.Wrap
                },
                MaskSpeechContent = maskText,
                OverlaySpeechContent = overlayText,
            });
        });
    }
}
