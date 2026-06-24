using System.IO;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SatoriMessagePlugin.Controls;
using SatoriMessagePlugin.Models;
using SatoriMessagePlugin.Services;
using SatoriMessagePlugin.Views.SettingsPages;

namespace SatoriMessagePlugin;

[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>
    /// 最新收到的消息信息，供组件进行数据绑定
    /// </summary>
    public SatoriMessageInfo LatestMessage { get; set; } = new();

    /// <summary>
    /// Satori 连接与过滤设置
    /// </summary>
    public SatoriConnectionSettings ConnectionSettings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 加载设置
        ConnectionSettings = ConfigureFileHelper.LoadConfig<SatoriConnectionSettings>(
            Path.Combine(PluginConfigFolder, "SatoriSettings.json"));

        // 设置变更时自动保存
        ConnectionSettings.PropertyChanged += (_, _) =>
        {
            ConfigureFileHelper.SaveConfig(
                Path.Combine(PluginConfigFolder, "SatoriSettings.json"),
                ConnectionSettings);
        };

        // 注册单例，让 DI 容器管理
        services.AddSingleton(ConnectionSettings);
        services.AddSingleton(this);

        // 注册 Satori WebSocket 后台服务
        services.AddHostedService<SatoriWebSocketService>();

        // 注册设置页面
        services.AddSettingsPage<SatoriSettingsPage>();
    }
}
