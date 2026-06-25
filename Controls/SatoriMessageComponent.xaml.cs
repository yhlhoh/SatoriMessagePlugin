using Avalonia.Controls;

namespace SatoriMessagePlugin.Controls;

public partial class SatoriMessageComponent : UserControl
{
    public Plugin Plugin { get; }

    public SatoriMessageComponent(Plugin plugin)
    {
        Plugin = plugin;
        DataContext = this;
        InitializeComponent();
    }
}
