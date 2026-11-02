using System.Windows;
using System.Windows.Controls;

namespace OreYeeter.TorchHost
{
    public partial class OreYeeterControl : UserControl
    {
        private OreYeeterPlugin Plugin { get; }

        private OreYeeterControl()
        {
            InitializeComponent();
        }

        public OreYeeterControl(OreYeeterPlugin plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
        }
    }
}
