using System;
using System.IO;
using System.Windows.Controls;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Plugins;

namespace OreYeeter.TorchHost
{
    public class OreYeeterPlugin : TorchPluginBase, IWpfPlugin, IOreYeeterHost
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private const string ConfigFileName = "OreYeeterConfig.cfg";

        private OreYeeterControl _control;
        private Persistent<OreYeeterConfig> _config;

        public OreYeeterConfig Config => _config?.Data;
        public UserControl GetControl() => _control ?? (_control = new OreYeeterControl(this));

        // IOreYeeterHost
        IOreConfig IOreYeeterHost.Config => Config;
        string IOreYeeterHost.StorageDir => StoragePath;
        bool IOreYeeterHost.IsClusterNode => false;
        void IOreYeeterHost.SaveConfig() => Save();
        void IOreYeeterHost.LogInfo(string message) => Log.Info(message);
        void IOreYeeterHost.LogError(Exception exception, string message) => Log.Error(exception, message);

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            SetupConfig();
            OreYeeterCore.Host = this;
            // Torch finds the [PatchShim] classes in this assembly by itself, nothing to register here.
            Log.Info("OreYeeter loaded (Torch)");
        }

        private void SetupConfig()
        {
            var configFile = Path.Combine(StoragePath, ConfigFileName);
            try
            {
                _config = Persistent<OreYeeterConfig>.Load(configFile);
            }
            catch (Exception e)
            {
                Log.Warn(e, "Could not read the config");
            }

            if (_config?.Data == null)
            {
                Log.Info("Creating default config");
                _config = new Persistent<OreYeeterConfig>(configFile, new OreYeeterConfig());
            }
            Save();
        }

        public void Save()
        {
            try
            {
                _config.Save();
            }
            catch (IOException e)
            {
                Log.Warn(e, "Configuration failed to save");
            }
        }

        public void Reload()
        {
            SetupConfig();
            _control = null;
        }
    }
}
