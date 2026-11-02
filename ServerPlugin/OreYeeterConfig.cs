using PluginSdk.Config;

namespace OreYeeter.MagnetarHost
{
    /// <summary>Magnetar config. Quasar renders the editor from these attributes, there is no UI code.</summary>
    [Tab("general", caption: "General")]
    public class OreYeeterConfig : PluginConfig, IOreConfig
    {
        private bool _enabled = true;
        private string _drillNameTag = OreYeeterCore.DefaultDrillNameTag;
        private string _masterOres = OreYeeterCore.DefaultMasterOres;
        private string _defaultPlayerOres = OreYeeterCore.DefaultPlayerOres;
        private int _cleanCooldownSeconds = OreYeeterCore.DefaultCleanCooldownSeconds;

        [BoolOption("Enable OreYeeter (the drill patch and the !ore commands)", Parent = "general")]
        public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

        [StringOption(maxLength: 64, description: "A ship drill whose name contains this tag voids its owner's ores", Parent = "general")]
        public string DrillNameTag { get => _drillNameTag; set => SetField(ref _drillNameTag, value ?? ""); }

        [StringOption(maxLength: 1000, description: "Comma separated ore subtypes voided for EVERY player, e.g. Stone,Ice. Players cannot remove these", Parent = "general")]
        public string MasterOres { get => _masterOres; set => SetField(ref _masterOres, value ?? ""); }

        [StringOption(maxLength: 1000, description: "Comma separated ore list used for players who have not changed their own list yet", Parent = "general")]
        public string DefaultPlayerOres { get => _defaultPlayerOres; set => SetField(ref _defaultPlayerOres, value ?? ""); }

        [IntOption(0, 3600, "Cooldown in seconds for !ore clean per player, 0 disables it", Parent = "general")]
        public int CleanCooldownSeconds { get => _cleanCooldownSeconds; set => SetField(ref _cleanCooldownSeconds, value); }
    }
}
