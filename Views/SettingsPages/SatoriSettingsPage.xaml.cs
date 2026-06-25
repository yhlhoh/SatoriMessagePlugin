using ClassIsland.Core.Abstractions.Controls;

namespace SatoriMessagePlugin.Views.SettingsPages;

public partial class SatoriSettingsPage : SettingsPageBase
{
    public Plugin Plugin { get; }
    public Models.SatoriConnectionSettings Settings => Plugin.ConnectionSettings;

    public SatoriSettingsPage(Plugin plugin)
    {
        Plugin = plugin;
        DataContext = this;
        InitializeComponent();
    }
}
