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
    public SatoriMessageInfo LatestMessage { get; set; } = new();
    public SatoriConnectionSettings ConnectionSettings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ConnectionSettings = ConfigureFileHelper.LoadConfig<SatoriConnectionSettings>(
            Path.Combine(PluginConfigFolder, "SatoriSettings.json"));

        ConnectionSettings.PropertyChanged += (_, _) =>
        {
            ConfigureFileHelper.SaveConfig(
                Path.Combine(PluginConfigFolder, "SatoriSettings.json"),
                ConnectionSettings);
        };

        services.AddSingleton(ConnectionSettings);
        services.AddSingleton(this);
        services.AddHostedService<SatoriWebSocketService>();
        services.AddSettingsPage<SatoriSettingsPage>();
    }
}
