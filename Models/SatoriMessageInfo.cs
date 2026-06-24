using CommunityToolkit.Mvvm.ComponentModel;

namespace SatoriMessagePlugin.Models;

/// <summary>
/// 消息信息模型，用于组件绑定。
/// 区分私聊/群聊格式：私聊="联系人:消息"；群聊="发件人(群名):消息"
/// </summary>
public class SatoriMessageInfo : ObservableRecipient
{
    private string _senderId = "";
    private string _senderName = "";
    private string _senderNickname = "";
    private string _content = "";
    private string _channelId = "";
    private string _channelName = "";
    private string _channelType = "";
    private string _guildId = "";
    private string _guildName = "";
    private string _platform = "";
    private DateTime _timestamp = DateTime.Now;
    private string _messageId = "";

    /// <summary>发送人 ID</summary>
    public string SenderId
    {
        get => _senderId;
        set
        {
            if (SetProperty(ref _senderId, value))
                OnPropertyChanged(nameof(DisplaySender));
        }
    }

    /// <summary>发送人用户名</summary>
    public string SenderName
    {
        get => _senderName;
        set
        {
            if (SetProperty(ref _senderName, value))
                OnPropertyChanged(nameof(DisplaySender));
        }
    }

    /// <summary>发送人昵称</summary>
    public string SenderNickname
    {
        get => _senderNickname;
        set
        {
            if (SetProperty(ref _senderNickname, value))
                OnPropertyChanged(nameof(DisplaySender));
        }
    }

    /// <summary>消息内容</summary>
    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
                OnPropertyChanged(nameof(FormattedDisplay));
        }
    }

    /// <summary>频道 ID</summary>
    public string ChannelId
    {
        get => _channelId;
        set => SetProperty(ref _channelId, value);
    }

    /// <summary>频道名称</summary>
    public string ChannelName
    {
        get => _channelName;
        set
        {
            if (SetProperty(ref _channelName, value))
                OnPropertyChanged(nameof(FormattedDisplay));
        }
    }

    /// <summary>频道类型 (TEXT=0, DIRECT=1, ...)</summary>
    public string ChannelType
    {
        get => _channelType;
        set
        {
            if (SetProperty(ref _channelType, value))
            {
                OnPropertyChanged(nameof(IsPrivateChat));
                OnPropertyChanged(nameof(FormattedDisplay));
            }
        }
    }

    /// <summary>群组 ID</summary>
    public string GuildId
    {
        get => _guildId;
        set => SetProperty(ref _guildId, value);
    }

    /// <summary>群组名称</summary>
    public string GuildName
    {
        get => _guildName;
        set
        {
            if (SetProperty(ref _guildName, value))
                OnPropertyChanged(nameof(FormattedDisplay));
        }
    }

    /// <summary>平台标识</summary>
    public string Platform
    {
        get => _platform;
        set => SetProperty(ref _platform, value);
    }

    /// <summary>消息时间戳</summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set
        {
            if (SetProperty(ref _timestamp, value))
                OnPropertyChanged(nameof(DisplayTime));
        }
    }

    /// <summary>消息 ID</summary>
    public string MessageId
    {
        get => _messageId;
        set => SetProperty(ref _messageId, value);
    }

    // ---- 派生属性 ----

    /// <summary>是否私聊（channel.type == "1" 即 DIRECT）</summary>
    public bool IsPrivateChat => ChannelType == "1";

    /// <summary>用于 UI 显示的发送人标识（昵称 > 用户名 > ID）</summary>
    public string DisplaySender =>
        !string.IsNullOrEmpty(SenderNickname) ? SenderNickname :
        !string.IsNullOrEmpty(SenderName) ? SenderName :
        SenderId;

    /// <summary>群名标识（guild.name > channel.name > guild.id）</summary>
    public string DisplayGroupName =>
        !string.IsNullOrEmpty(GuildName) ? GuildName :
        !string.IsNullOrEmpty(ChannelName) ? ChannelName :
        GuildId;

    /// <summary>
    /// 格式化后的完整显示文本：
    /// 私聊: "联系人:消息"
    /// 群聊: "发件人(群名):消息"
    /// </summary>
    public string FormattedDisplay =>
        IsPrivateChat
            ? $"{DisplaySender}: {Content}"
            : $"{DisplaySender}({DisplayGroupName}): {Content}";

    /// <summary>格式化后的接收时间</summary>
    public string DisplayTime => Timestamp.ToString("HH:mm:ss");
}
