using System.Net.WebSockets;
using System.Text;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Models.Notification;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using SatoriMessagePlugin.Models;

namespace SatoriMessagePlugin.Services;

public class SatoriWebSocketService : IHostedService
{
    private readonly Plugin _plugin;
    private readonly SatoriConnectionSettings _settings;
    private readonly INotificationSender? _notificationSender;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;

    private HashSet<string> _blockedUserIds = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _blockedChannelIds = new(StringComparer.OrdinalIgnoreCase);

    public SatoriWebSocketService(
        Plugin plugin,
        SatoriConnectionSettings settings,
        INotificationSender? notificationSender = null)
    {
        _plugin = plugin;
        _settings = settings;
        _notificationSender = notificationSender;
        RefreshBlocklists();

        _settings.PropertyChanged += (_, _) => RefreshBlocklists();
    }

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
            var t = line.Trim();
            if (!string.IsNullOrEmpty(t)) set.Add(t);
        }
        return set;
    }

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
                System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] {ex.Message}");
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
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_settings.Token}");

        await _webSocket.ConnectAsync(new Uri(_settings.WebSocketUrl), ct);
        System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] 已连接 {_settings.WebSocketUrl}");
    }

    private async Task DisconnectAsync()
    {
        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                _webSocket.Dispose();
            }
            catch { }
            _webSocket = null;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_webSocket == null) return;
        var buffer = new byte[65536];
        var sb = new StringBuilder();

        while (_webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            sb.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                    return;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text)
                ProcessMessage(sb.ToString());
        }
    }

    private void ProcessMessage(string rawJson)
    {
        try
        {
            var json = JObject.Parse(rawJson);
            var eventType = json["type"]?.ToString();
            if (eventType is not ("message-created" or "message-updated")) return;

            var channel = json["channel"];
            var user = json["user"];
            var message = json["message"];
            var guild = json["guild"];
            if (message == null || user == null || channel == null) return;

            var senderId = user["id"]?.ToString() ?? "";
            var channelId = channel["id"]?.ToString() ?? "";
            var channelType = channel["type"]?.ToString() ?? "0";

            if (_blockedUserIds.Contains(senderId)) return;
            if (_blockedChannelIds.Contains(channelId)) return;

            var senderName = user["name"]?.ToString() ?? "";
            var senderNickname = user["nick"]?.ToString() ?? "";
            var content = message["content"]?.ToString() ?? "";
            var messageId = message["id"]?.ToString() ?? "";
            var channelName = channel["name"]?.ToString() ?? "";
            var guildId = guild?["id"]?.ToString() ?? "";
            var guildName = guild?["name"]?.ToString() ?? "";
            var platform = json["platform"]?.ToString() ?? "";

            if (_settings.MaxContentLength > 0 && content.Length > _settings.MaxContentLength)
                content = content[.._settings.MaxContentLength] + "...";

            var timestamp = DateTime.Now;
            var isPrivate = channelType == "1";

            Dispatcher.UIThread.Post(() =>
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

            if (_settings.EnableNotification)
                TriggerNotification(isPrivate);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SatoriPlugin] 解析失败: {ex.Message}");
        }
    }

    private void TriggerNotification(bool isPrivate)
    {
        if (_notificationSender == null) return;

        var m = _plugin.LatestMessage;
        var maskText = isPrivate
            ? m.DisplaySender
            : $"{m.DisplaySender}({m.DisplayGroupName})";
        var overlayText = m.Content;

        Dispatcher.UIThread.Post(() =>
        {
            _notificationSender.ShowNotification(new NotificationRequest()
            {
                MaskContent = NotificationContent.CreateSimpleTextContent(maskText),
                OverlayContent = NotificationContent.CreateSimpleTextContent(overlayText),
            });
        });
    }
}
