using CommunityToolkit.Mvvm.ComponentModel;

namespace SatoriMessagePlugin.Models;

public class SatoriConnectionSettings : ObservableRecipient
{
    private string _webSocketUrl = "ws://127.0.0.1:5140/";
    private string _token = "";
    private bool _enableNotification = true;
    private string _blockedUserIds = "";
    private string _blockedChannelIds = "";
    private bool _showTimestamp = true;
    private int _maxContentLength = 200;

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
