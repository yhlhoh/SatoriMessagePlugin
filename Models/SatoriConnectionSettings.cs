using CommunityToolkit.Mvvm.ComponentModel;

namespace SatoriMessagePlugin.Models;

/// <summary>
/// Satori 连接与过滤设置
/// </summary>
public class SatoriConnectionSettings : ObservableRecipient
{
    private string _webSocketUrl = "ws://127.0.0.1:5140/";
    private string _token = "";
    private bool _enableNotification = true;
    private string _blockedUserIds = "";      // 每行一个用户 ID
    private string _blockedChannelIds = "";   // 每行一个频道 ID
    private bool _showTimestamp = true;
    private int _maxContentLength = 200;

    /// <summary>
    /// Satori WebSocket 服务器地址
    /// </summary>
    public string WebSocketUrl
    {
        get => _webSocketUrl;
        set
        {
            if (value == _webSocketUrl) return;
            _webSocketUrl = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 鉴权 Token（可选，留空则不发送）
    /// </summary>
    public string Token
    {
        get => _token;
        set
        {
            if (value == _token) return;
            _token = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 收到消息时是否触发 ClassIsland 原生提醒
    /// </summary>
    public bool EnableNotification
    {
        get => _enableNotification;
        set
        {
            if (value == _enableNotification) return;
            _enableNotification = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 屏蔽的用户 ID 列表（每行一个），私聊中来自这些用户的消息将被忽略
    /// </summary>
    public string BlockedUserIds
    {
        get => _blockedUserIds;
        set
        {
            if (value == _blockedUserIds) return;
            _blockedUserIds = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 屏蔽的频道 ID 列表（每行一个），这些频道中的消息将被忽略
    /// </summary>
    public string BlockedChannelIds
    {
        get => _blockedChannelIds;
        set
        {
            if (value == _blockedChannelIds) return;
            _blockedChannelIds = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否显示时间戳
    /// </summary>
    public bool ShowTimestamp
    {
        get => _showTimestamp;
        set
        {
            if (value == _showTimestamp) return;
            _showTimestamp = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 消息内容最大显示长度
    /// </summary>
    public int MaxContentLength
    {
        get => _maxContentLength;
        set
        {
            if (value == _maxContentLength) return;
            _maxContentLength = value;
            OnPropertyChanged();
        }
    }
}
