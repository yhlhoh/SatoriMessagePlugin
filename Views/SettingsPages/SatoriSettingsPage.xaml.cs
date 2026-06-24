using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using MaterialDesignThemes.Wpf;

namespace SatoriMessagePlugin.Views.SettingsPages;

/// <summary>
/// Satori 消息插件设置页面
/// </summary>
[SettingsPageInfo("satori.settings", "Satori 消息", PackIconKind.MessageTextOutline, PackIconKind.MessageText)]
public partial class SatoriSettingsPage : SettingsPageBase
{
    /// <summary>
    /// 当前插件实例，用于绑定到设置属性
    /// </summary>
    public Plugin Plugin { get; }

    /// <summary>
    /// 连接到 Plugin.ConnectionSettings 的快捷绑定入口
    /// </summary>
    public Models.SatoriConnectionSettings Settings => Plugin.ConnectionSettings;

    public SatoriSettingsPage(Plugin plugin)
    {
        Plugin = plugin;
        DataContext = this;
        InitializeComponent();
    }
}
