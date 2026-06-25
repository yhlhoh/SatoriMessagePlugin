using Avalonia;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using SatoriMessagePlugin.Models;

namespace SatoriMessagePlugin.Controls;

[ComponentInfo(
    "8A3F4D2E-B1C5-4A7F-9E2D-6F8A0B3C1D4E",
    "Satori消息",
    "",
    "显示最近一条 Satori 消息。")]
public partial class SatoriMessageComponent : ComponentBase
{
    private SatoriMessageInfo? _latestMessage;

    public static readonly DirectProperty<SatoriMessageComponent, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<SatoriMessageComponent, string>(
            nameof(DisplayText), o => o.DisplayText);

    private string _displayText = "没有最近消息";

    public string DisplayText
    {
        get => _displayText;
        private set => SetAndRaise(DisplayTextProperty, ref _displayText, value);
    }

    public SatoriMessageComponent()
    {
        InitializeComponent();
    }

    private void Visual_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_latestMessage == null)
        {
            try
            {
                _latestMessage = IAppHost.GetService<SatoriMessageInfo>();
            }
            catch
            {
                _latestMessage = null;
            }
        }

        if (_latestMessage != null)
        {
            _latestMessage.PropertyChanged += OnMessageChanged;
        }

        UpdateContent();
    }

    private void Visual_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_latestMessage != null)
        {
            _latestMessage.PropertyChanged -= OnMessageChanged;
        }
    }

    private void OnMessageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SatoriMessageInfo.DisplayText)
            || e.PropertyName == nameof(SatoriMessageInfo.HasMessage))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateContent);
        }
    }

    private void UpdateContent()
    {
        if (_latestMessage == null)
        {
            DisplayText = "没有最近消息";
            return;
        }

        DisplayText = _latestMessage.DisplayText;
    }
}
