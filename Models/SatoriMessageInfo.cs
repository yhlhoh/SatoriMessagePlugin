using CommunityToolkit.Mvvm.ComponentModel;

namespace SatoriMessagePlugin.Models;

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

    public string SenderId
    {
        get => _senderId;
        set
        {
            if (SetProperty(ref _senderId, value))
                OnPropertyChanged(nameof(DisplaySender));
        }
    }

    public string SenderName
    {
        get => _senderName;
        set
        {
            if (SetProperty(ref _senderName, value))
                OnPropertyChanged(nameof(DisplaySender));
        }
    }

    public string SenderNickname
    {
        get => _senderNickname;
        set
        {
            if (SetProperty(ref _senderNickname, value))
                OnPropertyChanged(nameof(DisplaySender));
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
                OnPropertyChanged(nameof(FormattedDisplay));
        }
    }

    public string ChannelId
    {
        get => _channelId;
        set => SetProperty(ref _channelId, value);
    }

    public string ChannelName
    {
        get => _channelName;
        set
        {
            if (SetProperty(ref _channelName, value))
                OnPropertyChanged(nameof(FormattedDisplay));
        }
    }

    public string ChannelType
    {
        get => _channelType;
        set
        {
            if (SetProperty(ref _channelType, value))
            {
                OnPropertyChanged(nameof(IsPrivateChat));
                OnPropertyChanged(nameof(IsGroupChat));
                OnPropertyChanged(nameof(FormattedDisplay));
            }
        }
    }

    public string GuildId
    {
        get => _guildId;
        set => SetProperty(ref _guildId, value);
    }

    public string GuildName
    {
        get => _guildName;
        set
        {
            if (SetProperty(ref _guildName, value))
                OnPropertyChanged(nameof(FormattedDisplay));
        }
    }

    public string Platform
    {
        get => _platform;
        set => SetProperty(ref _platform, value);
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set
        {
            if (SetProperty(ref _timestamp, value))
                OnPropertyChanged(nameof(DisplayTime));
        }
    }

    public string MessageId
    {
        get => _messageId;
        set => SetProperty(ref _messageId, value);
    }

    public bool IsPrivateChat => ChannelType == "1";

    public bool IsGroupChat => ChannelType != "1";

    public string DisplaySender =>
        !string.IsNullOrEmpty(SenderNickname) ? SenderNickname :
        !string.IsNullOrEmpty(SenderName) ? SenderName :
        SenderId;

    public string DisplayGroupName =>
        !string.IsNullOrEmpty(GuildName) ? GuildName :
        !string.IsNullOrEmpty(ChannelName) ? ChannelName :
        GuildId;

    /// 私聊: "联系人:消息" / 群聊: "发件人(群名):消息"
    public string FormattedDisplay =>
        IsPrivateChat
            ? $"{DisplaySender}: {Content}"
            : $"{DisplaySender}({DisplayGroupName}): {Content}";

    public string DisplayTime => Timestamp.ToString("HH:mm:ss");
}
