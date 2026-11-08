using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using HarmonyLib;
using PluginSdk.Clustering;
using PluginSdk.Commands;
using PluginSdk.Config;
using PluginSdk.Logging;
using PluginSdk.Storage;
using VRage.FileSystem;
using VRage.Plugins;

// Define assembly version when compiled by Magnetar
#if !DEV_BUILD
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]
#endif

namespace OreYeeter.MagnetarHost
{
    // ReSharper disable once UnusedType.Global
    public class Plugin : IPlugin, IOreYeeterHost
    {
        public const string Name = "OreYeeter";
        // The Id of OreYeeter.xml
        public const string Id = "6e0b4a93-d1f7-4c28-b5a6-3f92c8d1e704";

        // Other nodes of a cluster write the shared player files, their changes are picked up this often
        private const int RefreshTicks = 300;
        private static readonly Logger Log = Logger.Create(Name);

        private OreYeeterConfig _config;
        private string _configPath;
        private string _storageDir;
        private bool _sharedStorage;
        private int _ticks;

        // Quasar finds the config through this public PluginConfig-typed property and renders its editor.
        public OreYeeterConfig PluginConfig => _config;

        // IOreYeeterHost
        IOreConfig IOreYeeterHost.Config => _config;
        string IOreYeeterHost.StorageDir => _storageDir;
        bool IOreYeeterHost.IsClusterNode => PluginCluster.IsClusterProcess;
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

            string shared = SharedStorageDir();
            _sharedStorage = shared != null;
            _storageDir = shared ?? Path.Combine(MyFileSystem.UserDataPath, Name);

            OreYeeterCore.Host = this;

            try
            {
                new Harmony(Name).PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                // A game update that changed the patched method must not take the server down, the plugin just stays inactive:
                // no patch, no commands. Enabled is left alone, it is saved to the config and would outlive the next fixed version.
                Log.Critical("Failed to apply the Harmony patches, OreYeeter is inactive", e);
                return;
            }

            ServerCommands.Register(Assembly.GetExecutingAssembly());
            Log.Info("Loaded (Magnetar)");
        }

        public void Update()
        {
            if (_sharedStorage && ++_ticks % RefreshTicks == 0)
                PlayerOreStore.Refresh();
        }

        public void Dispose()
        {
            // IMPORTANT: Do NOT call harmony.UnpatchAll() here, it may break other plugins.
            if (_config != null)
                _config.PropertyChanged -= OnConfigChanged;
        }

        // The nodes of a cluster share this folder, so a player's list follows them from node to node. Null on a plain
        // server, and on a cluster that cannot use it, like one on several hosts with no shared mount or with a bad mount.
        private static string SharedStorageDir()
        {
            if (!PluginCluster.IsClusterProcess)
                return null;
            try
            {
                return PluginStorage.GetSharedDirectory(Id);
            }
            catch (Exception e)
            {
                Log.Warning($"Player lists stay on this node, no shared folder: {e.Message}");
                return null;
            }
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
