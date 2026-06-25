using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
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
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var settings = SatoriConnectionSettings.Load(PluginConfigFolder);
        settings.Normalize();
        TrySaveSettings(settings);

        services.AddSingleton(settings);
        services.AddSingleton<SatoriMessageInfo>();
        services.AddSingleton<SatoriWebSocketService>();

        services.AddNotificationProvider<SatoriWebSocketService>();
        services.AddComponent<SatoriMessageComponent>();
        services.AddSettingsPage<SatoriSettingsPage>();
    }

    private static void TrySaveSettings(SatoriConnectionSettings settings)
    {
        try
        {
            settings.Save();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SatoriMessagePlugin] 回写设置失败: {ex.Message}");
        }
    }
}
