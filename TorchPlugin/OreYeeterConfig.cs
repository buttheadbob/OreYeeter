using Torch;

namespace OreYeeter.TorchHost
{
    /// <summary>Torch config (WPF bound, saved as OreYeeterConfig.cfg). The property docs live on IOreConfig.</summary>
    public class OreYeeterConfig : ViewModel, IOreConfig
    {
        private bool _enabled = true;
        private string _drillNameTag = OreYeeterCore.DefaultDrillNameTag;
        // Stored as strings (not lists) because XmlSerializer appends to a pre-filled list on load,
        // which would duplicate the default on every restart.
        private string _masterOres = OreYeeterCore.DefaultMasterOres;
        private string _defaultPlayerOres = OreYeeterCore.DefaultPlayerOres;
        private int _cleanCooldownSeconds = OreYeeterCore.DefaultCleanCooldownSeconds;

        public bool Enabled { get => _enabled; set => SetValue(ref _enabled, value); }
        public string DrillNameTag { get => _drillNameTag; set => SetValue(ref _drillNameTag, value ?? ""); }
        public string MasterOres { get => _masterOres; set => SetValue(ref _masterOres, value ?? ""); }
        public string DefaultPlayerOres { get => _defaultPlayerOres; set => SetValue(ref _defaultPlayerOres, value ?? ""); }
        public int CleanCooldownSeconds { get => _cleanCooldownSeconds; set => SetValue(ref _cleanCooldownSeconds, value); }
    }
}
