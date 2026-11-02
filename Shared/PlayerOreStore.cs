using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Sandbox.Game.World;

namespace OreYeeter
{
    /// <summary>One player's settings. Instances are never edited after being stored, changes swap in a new one.</summary>
    public sealed class PlayerSettings
    {
        public HashSet<string> Ores { get; }

        /// <summary>When true all of the player's ship drills yeet, not only the ones carrying the name tag.</summary>
        public bool AutoDrills { get; }

        public PlayerSettings(HashSet<string> ores, bool autoDrills)
        {
            Ores = ores;
            AutoDrills = autoDrills;
        }
    }

    /// <summary>
    /// Per player settings, one file per player in &lt;host storage&gt;\PlayerOres\&lt;steamid&gt;.json.
    /// A file is only read the first time the player is needed, then kept in memory. Players without a file use the
    /// configured default list (nothing is written until they change something).
    /// </summary>
    public static class PlayerOreStore
    {
        private const string FolderName = "PlayerOres";

        // identity id -> steam id. Zero (NPCs, not yet mapped) is not cached so a later lookup can still succeed.
        private static readonly ConcurrentDictionary<long, ulong> SteamIds = new ConcurrentDictionary<long, ulong>();
        private static readonly ConcurrentDictionary<ulong, PlayerSettings> Cache = new ConcurrentDictionary<ulong, PlayerSettings>();
        private static readonly object WriteLock = new object();

        private static string Folder => Path.Combine(OreYeeterCore.Host.StorageDir, FolderName);
        private static string FilePath(ulong steamId) => Path.Combine(Folder, steamId + ".json");

        public static ulong GetSteamId(long identityId)
        {
            if (identityId == 0)
                return 0;
            if (SteamIds.TryGetValue(identityId, out ulong steamId))
                return steamId;

            steamId = MySession.Static?.Players?.TryGetSteamId(identityId) ?? 0;
            if (steamId != 0)
                SteamIds[identityId] = steamId;
            return steamId;
        }

        public static PlayerSettings GetForIdentity(long identityId)
        {
            ulong steamId = GetSteamId(identityId);
            return steamId == 0 ? null : Get(steamId);
        }

        public static PlayerSettings Get(ulong steamId) => Cache.GetOrAdd(steamId, Load);

        /// <summary>Applies a change, updating memory first and then the file.</summary>
        public static PlayerSettings Update(ulong steamId, Func<PlayerSettings, PlayerSettings> change)
        {
            lock (WriteLock)
            {
                var updated = change(Get(steamId));
                Cache[steamId] = updated;
                Save(steamId, updated);
                return updated;
            }
        }

        private static PlayerSettings Load(ulong steamId)
        {
            var host = OreYeeterCore.Host;
            var defaults = new PlayerSettings(new HashSet<string>(OreRules.GetDefaults(host.Config), StringComparer.OrdinalIgnoreCase), false);
            string file = FilePath(steamId);
            try
            {
                if (!File.Exists(file))
                    return defaults;

                using (var stream = File.OpenRead(file))
                {
                    var data = (PlayerFile)new DataContractJsonSerializer(typeof(PlayerFile)).ReadObject(stream);
                    var ores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in data?.Ores ?? new List<string>())
                    {
                        var subtype = OreRules.ToOreSubtype(entry);
                        if (subtype.Length > 0)
                            ores.Add(subtype);
                    }
                    return new PlayerSettings(ores, data?.AutoDrills ?? false);
                }
            }
            catch (Exception e)
            {
                // A broken file is treated as the defaults for this session; it is rewritten when the player changes something.
                host.LogError(e, $"Could not read {file}, using defaults");
                return defaults;
            }
        }

        private static void Save(ulong steamId, PlayerSettings settings)
        {
            string file = FilePath(steamId);
            string temp = file + ".tmp";
            try
            {
                Directory.CreateDirectory(Folder);
                // Write to a temp file and swap it in, so a crash mid write cannot leave a half written file.
                using (var stream = File.Create(temp))
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true))
                {
                    new DataContractJsonSerializer(typeof(PlayerFile)).WriteObject(writer,
                        new PlayerFile { Ores = settings.Ores.OrderBy(x => x).ToList(), AutoDrills = settings.AutoDrills });
                }
                if (File.Exists(file))
                    File.Replace(temp, file, null);
                else
                    File.Move(temp, file);
            }
            catch (Exception e)
            {
                OreYeeterCore.Host.LogError(e, $"Could not save {file}");
            }
        }

        // file layout: { "AutoDrills": true, "Ores": [ "Stone", "Ice" ] }
        [DataContract]
        private class PlayerFile
        {
            [DataMember(Name = "AutoDrills")]
            public bool AutoDrills { get; set; }

            [DataMember(Name = "Ores")]
            public List<string> Ores { get; set; }
        }
    }
}
