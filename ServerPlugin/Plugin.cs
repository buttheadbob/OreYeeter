using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using HarmonyLib;
using PluginSdk.Commands;
using PluginSdk.Config;
using PluginSdk.Logging;
using VRage.FileSystem;
using VRage.Plugins;

// Define assembly version when compiled by Magnetar
#if !DEV_BUILD
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#endif

namespace OreYeeter.MagnetarHost
{
    // ReSharper disable once UnusedType.Global
    public class Plugin : IPlugin, IOreYeeterHost
    {
        public const string Name = "OreYeeter";
        private static readonly Logger Log = Logger.Create(Name);

        private OreYeeterConfig _config;
        private string _configPath;

        // Quasar finds the config through this public PluginConfig-typed property and renders its editor.
        public OreYeeterConfig PluginConfig => _config;

        // IOreYeeterHost
        IOreConfig IOreYeeterHost.Config => _config;
        string IOreYeeterHost.StorageDir => Path.Combine(MyFileSystem.UserDataPath, Name);
        void IOreYeeterHost.SaveConfig() => SaveConfig();
        void IOreYeeterHost.LogInfo(string message) => Log.Info(message);
        void IOreYeeterHost.LogError(Exception exception, string message) => Log.Error(message, exception);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {
            Log.Info("Loading");

            _configPath = Path.Combine(MyFileSystem.UserDataPath, Name + ".cfg");
            try
            {
                _config = ConfigStorage.LoadXml<OreYeeterConfig>(_configPath);
            }
            catch (Exception e)
            {
                Log.Error("Could not read the config, using defaults", e);
                _config = new OreYeeterConfig();
            }
            // Edits made in Quasar arrive through the config's setters, persist them
            _config.PropertyChanged += OnConfigChanged;

            OreYeeterCore.Host = this;

            try
            {
                new Harmony(Name).PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                // A game update that changed the patched method must not take the server down, the plugin just stays inactive
                Log.Critical("Failed to apply the Harmony patches, OreYeeter is inactive", e);
                _config.Enabled = false;
                return;
            }

            ServerCommands.Register(Assembly.GetExecutingAssembly());
            Log.Info("Loaded (Magnetar)");
        }

        public void Update()
        {
        }

        public void Dispose()
        {
            // IMPORTANT: Do NOT call harmony.UnpatchAll() here, it may break other plugins.
            if (_config != null)
                _config.PropertyChanged -= OnConfigChanged;
        }

        private void OnConfigChanged(object sender, PropertyChangedEventArgs e) => SaveConfig();

        private void SaveConfig()
        {
            try
            {
                ConfigStorage.SaveXml(_config, _configPath);
            }
            catch (Exception e)
            {
                Log.Error("Could not save the config", e);
            }
        }
    }
}
