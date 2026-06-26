using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SatoriMessagePlugin.Models;

/// <summary>
/// 存储最新一条 Satori 消息的状态，供组件绑定显示。
/// </summary>
public partial class SatoriMessageInfo : ObservableObject
{
    [ObservableProperty]
    private string _sender = "";

    [ObservableProperty]
    private string _content = "";

    [ObservableProperty]
    private string _groupName = "";

    [ObservableProperty]
    private DateTimeOffset _receivedAt;

    [ObservableProperty]
    private bool _hasMessage;

    /// <summary>是否有群名（即是否为群消息）</summary>
    [ObservableProperty]
    private bool _isGroupMessage;

    /// <summary>用于显示的格式化文本</summary>
    public string DisplayText
    {
        get
        {
            if (!HasMessage)
                return "没有最近消息";

            var sender = string.IsNullOrWhiteSpace(Sender) ? "未知" : Sender;
            var content = FormatContent(Content); // 使用格式化后的内容

            if (IsGroupMessage && !string.IsNullOrWhiteSpace(GroupName))
                return $"{sender}({GroupName}):{content}";

            return $"{sender}：{content}";
        }
    }

    /// <summary>通知正文</summary>
    public string NotificationBody => string.IsNullOrWhiteSpace(Content)
        ? NotificationTitle
        : FormatContent(Content); // 使用格式化后的内容

    /// <summary>
    /// 格式化消息内容：换行替换为空格，并截断至 20 个字符（超出显示 ...）。
    /// </summary>
    private string FormatContent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw ?? "";

        // 换行转空格
        var processed = raw.Replace("\r\n", " ").Replace("\n", " ");

        // 20 字符截断
        if (processed.Length > 20)
            return processed.Substring(0, 20) + "...";

        return processed;
    }

    /// <summary>从 Satori message body JSON 解析消息</summary>
    public void UpdateFromSatoriBody(JsonObject body)
    {
        // 解析 user
        var user = body["user"]?.AsObject();
        var sender = "";
        if (user != null)
        {
            sender = user["nick"]?.GetValue<string>()
                  ?? user["name"]?.GetValue<string>()
                  ?? user["id"]?.GetValue<string>()
                  ?? "";
        }

        // 解析 channel
        var channel = body["channel"]?.AsObject();
        var groupName = "";
        if (channel != null)
        {
            groupName = channel["name"]?.GetValue<string>() ?? "";
        }

        // 如果 channel 没有 name，尝试 guild
        if (string.IsNullOrWhiteSpace(groupName))
        {
            var guild = body["guild"]?.AsObject();
            if (guild != null)
            {
                groupName = guild["name"]?.GetValue<string>() ?? "";
            }
        }

        // 解析 content - 支持 "content" 和 "message.content" 两种格式
        var content = body["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            var message = body["message"]?.AsObject();
            if (message != null)
            {
                content = message["content"]?.GetValue<string>() ?? "";
            }
        }
        content ??= "";

        // 解析时间
        var createdAt = body["createdAt"]?.GetValue<long>() ?? 0;
        var receivedAt = createdAt > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(createdAt)
            : DateTimeOffset.Now;

        // 更新属性 — 直接写 backing field 以避免中间 PropertyChanged 通知，
        // 最后统一触发，保证 UI 原子更新。
#pragma warning disable MVVMTK0034
        _sender = sender;
        _content = content;          // 保留原始内容
        _groupName = groupName;
        _receivedAt = receivedAt;
        _hasMessage = true;
        _isGroupMessage = !string.IsNullOrWhiteSpace(groupName);
#pragma warning restore MVVMTK0034

        // 批量通知 UI 更新
        OnPropertyChanged(nameof(Sender));
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(ReceivedAt));
        OnPropertyChanged(nameof(HasMessage));
        OnPropertyChanged(nameof(IsGroupMessage));
        OnPropertyChanged(nameof(DisplayText));
    }

    /// <summary>获取消息用于去重的唯一键</summary>
    public string GetDeduplicationKey()
    {
        var bodyId = Content ?? "";
        return $"{Sender}|{GroupName}|{bodyId}|{ReceivedAt:yyyyMMddHHmm}";
    }

    /// <summary>通知标题</summary>
    public string NotificationTitle => IsGroupMessage
        ? $"{Sender} ({GroupName})"
        : Sender;
}
