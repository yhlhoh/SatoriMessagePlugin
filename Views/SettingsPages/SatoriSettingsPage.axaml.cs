using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using SatoriMessagePlugin.Models;
using SatoriMessagePlugin.Services;

namespace SatoriMessagePlugin.Views.SettingsPages;

[SettingsPageInfo("satorimessage.settings", "Satori消息", "")]
public partial class SatoriSettingsPage : SettingsPageBase, INotifyPropertyChanged
{
    private readonly SatoriConnectionSettings _settings;
    private readonly SatoriWebSocketService? _wsService;
    private DispatcherTimer? _saveTimer;
    private DispatcherTimer? _connectionPollTimer;

    public SatoriConnectionSettings Settings => _settings;

    public bool CanReconnect =>
        _settings.IsEnabled && !string.IsNullOrWhiteSpace(_settings.SatoriWsUrl);

    public string ConnectionStatusText
    {
        get
        {
            if (!_settings.IsEnabled) return "监听已禁用";
            if (string.IsNullOrWhiteSpace(_settings.SatoriWsUrl)) return "未配置 WebSocket 地址";
            if (_wsService?.IsConnected == true) return "已连接";
            return "未连接";
        }
    }

    public IBrush ConnectionStatusColor
    {
        get
        {
            if (!_settings.IsEnabled || string.IsNullOrWhiteSpace(_settings.SatoriWsUrl))
                return Brushes.Gray;
            if (_wsService?.IsConnected == true)
                return Brushes.LimeGreen;
            return Brushes.OrangeRed;
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public SatoriSettingsPage()
        : this(new SatoriConnectionSettings(), null, false)
    {
    }

    public SatoriSettingsPage(
        SatoriConnectionSettings settings,
        SatoriWebSocketService? wsService)
        : this(settings, wsService, true)
    {
    }

    private SatoriSettingsPage(
        SatoriConnectionSettings settings,
        SatoriWebSocketService? wsService,
        bool autoSaveEnabled)
    {
        _settings = settings;
        _wsService = wsService;
        DataContext = this;
        InitializeComponent();

        if (autoSaveEnabled)
        {
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            if (_wsService != null)
            {
                _wsService.OnConnectionStateChanged += OnConnectionStateChanged;
                // 定期刷新连接状态
                _connectionPollTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _connectionPollTimer.Tick += (_, _) => RefreshConnectionStatus();
                _connectionPollTimer.Start();
            }
        }
    }

    private void OnConnectionStateChanged()
    {
        Dispatcher.UIThread.Post(RefreshConnectionStatus);
    }

    private void RefreshConnectionStatus()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectionStatusText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectionStatusColor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanReconnect)));
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SatoriConnectionSettings.IsEnabled)
            or nameof(SatoriConnectionSettings.SatoriWsUrl))
        {
            RefreshConnectionStatus();
        }
        DebounceSave();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        if (_wsService != null)
        {
            _wsService.OnConnectionStateChanged -= OnConnectionStateChanged;
        }
        if (_saveTimer != null)
        {
            _saveTimer.Stop();
            _saveTimer.Tick -= OnSaveTimerTick;
            _saveTimer = null;
        }
        if (_connectionPollTimer != null)
        {
            _connectionPollTimer.Stop();
            _connectionPollTimer = null;
        }
    }

    private void DebounceSave()
    {
        if (_saveTimer == null)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveTimer.Tick += OnSaveTimerTick;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer?.Stop();
        _settings.Normalize();
        _settings.Save();
    }

    private async void OnReconnectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_wsService != null)
        {
            try
            {
                await _wsService.ReconnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SatoriMessagePlugin] 重连失败: {ex.Message}");
            }
        }
    }
}
