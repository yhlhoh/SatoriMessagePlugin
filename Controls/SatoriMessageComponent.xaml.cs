using System.Windows.Controls;

namespace SatoriMessagePlugin.Controls;

/// <summary>
/// Satori 消息显示组件：在 ClassIsland 界面上实时展示 Satori 协议收到的消息
/// </summary>
public partial class SatoriMessageComponent : UserControl
{
    /// <summary>
    /// 插件实例，用于绑定到 LatestMessage
    /// </summary>
    public Plugin Plugin { get; }

    public SatoriMessageComponent(Plugin plugin)
    {
        Plugin = plugin;
        DataContext = this;
        InitializeComponent();
    }
}
