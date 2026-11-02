using System;
using System.Collections.Generic;

// Engine neutral core, compiled into BOTH the Torch plugin and the Magnetar plugin.
// Rules for everything under Shared/:
//  - game assemblies (Sandbox.*, VRage.*) only: NO Torch, NLog, Harmony or PluginSdk references
//  - C# 8 syntax at most (the Torch project is limited to it), block scoped namespaces
//  - talk to the host plugin through IOreYeeterHost / IOreConfig only
namespace OreYeeter
{
    /// <summary>The settings both hosts expose (Torch: WPF + .cfg file, Magnetar: Quasar + XML).</summary>
    public interface IOreConfig
    {
        /// <summary>Master switch for the drill patch and the commands.</summary>
        bool Enabled { get; set; }

        /// <summary>A ship drill whose name contains this tag voids its owner's ores (case-insensitive).</summary>
        string DrillNameTag { get; set; }

        /// <summary>Comma separated ore subtypes voided for EVERY player, by tagged drills (or all drills of "!ore auto" players). Players cannot remove these.</summary>
        string MasterOres { get; set; }

        /// <summary>Comma separated personal list for players who never edited theirs.</summary>
        string DefaultPlayerOres { get; set; }

        /// <summary>Cooldown for "!ore clean" per player, 0 disables it.</summary>
        int CleanCooldownSeconds { get; set; }
    }

    /// <summary>What the core needs from whichever plugin loader it runs in.</summary>
    public interface IOreYeeterHost
    {
        IOreConfig Config { get; }

        /// <summary>Folder for per player files (created on demand).</summary>
        string StorageDir { get; }

        /// <summary>Persist the config after the core changed it (e.g. "!ore master add").</summary>
        void SaveConfig();

        void LogInfo(string message);
        void LogError(Exception exception, string message);
    }

    public static class OreYeeterCore
    {
        public const string DefaultDrillNameTag = "!ore";
        public const string DefaultMasterOres = "";
        public const string DefaultPlayerOres = "Stone";
        public const int DefaultCleanCooldownSeconds = 60;

        /// <summary>Set by the host plugin in Init, before any patch or command can run.</summary>
        public static IOreYeeterHost Host { get; set; }

        public static IOreConfig Config => Host?.Config;
    }

    /// <summary>Parsing and matching of ore subtype lists.</summary>
    public static class OreRules
    {
        private sealed class Cached
        {
            public string Source;
            public HashSet<string> Set;
        }

        // The drill patch reads these from the game's worker threads. Sets are rebuilt when the config string changes
        // and swapped in whole, never edited in place.
        private static Cached _master;
        private static Cached _defaults;

        public static HashSet<string> GetMaster(IOreConfig config) => Get(ref _master, config.MasterOres);
        public static HashSet<string> GetDefaults(IOreConfig config) => Get(ref _defaults, config.DefaultPlayerOres);

        private static HashSet<string> Get(ref Cached slot, string csv)
        {
            csv = csv ?? "";
            var cached = slot;
            if (cached != null && string.Equals(cached.Source, csv, StringComparison.Ordinal))
                return cached.Set;

            cached = new Cached { Source = csv, Set = Parse(csv) };
            slot = cached;
            return cached.Set;
        }

        public static HashSet<string> Parse(string csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in (csv ?? "").Split(','))
            {
                var subtype = ToOreSubtype(entry);
                if (subtype.Length > 0)
                    set.Add(subtype);
            }
            return set;
        }

        /// <summary>Accepts "Stone", "Ore/Stone" or "MyObjectBuilder_Ore/Stone".</summary>
        public static string ToOreSubtype(string entry)
        {
            return entry == null ? "" : entry.Substring(entry.LastIndexOf('/') + 1).Trim();
        }

        /// <summary>An ore is voided when it is on the server list or the owner's personal list.</summary>
        public static bool ShouldYeet(IOreConfig config, string ore, PlayerSettings settings)
        {
            return GetMaster(config).Contains(ore) || (settings != null && settings.Ores.Contains(ore));
        }
    }
}
