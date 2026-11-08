using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Game.World;

namespace OreYeeter
{
    /// <summary>One player's settings. Instances are never edited after being stored, changes swap in a new one.</summary>
    public sealed class PlayerSettings
    {
        public HashSet<string> Ores { get; }

        /// <summary>When true all of the player's ship drills yeet, not only the ones carrying the name tag.</summary>
        public bool AutoDrills { get; }

        /// <summary>True while the player has no file: Ores is then the configured default list and follows its changes.</summary>
        public bool UsesDefaults { get; }

        public PlayerSettings(HashSet<string> ores, bool autoDrills, bool usesDefaults = false)
        {
            Ores = ores;
            AutoDrills = autoDrills;
            UsesDefaults = usesDefaults;
        }
    }

    /// <summary>
    /// Per player settings, one file per player in &lt;host storage&gt;\PlayerOres\&lt;steamid&gt;.json.
    /// A file is read the first time the player is needed, then kept in memory. Servers that share the folder call
    /// Refresh to pick up each other's changes. Players without a file use the configured default list (nothing is
    /// written until they change something).
    /// </summary>
    public static class PlayerOreStore
    {
        private const string FolderName = "PlayerOres";

        // identity id -> steam id. Zero (NPCs, not yet mapped) is not cached so a later lookup can still succeed.
        private static readonly ConcurrentDictionary<long, ulong> SteamIds = new ConcurrentDictionary<long, ulong>();
        private static readonly ConcurrentDictionary<ulong, PlayerSettings> Cache = new ConcurrentDictionary<ulong, PlayerSettings>();
        // steam id -> last write time of the file the cached settings were read from or written to
        private static readonly ConcurrentDictionary<ulong, DateTime> FileTimes = new ConcurrentDictionary<ulong, DateTime>();
        private static readonly object WriteLock = new object();
        private static int _refreshing;

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

        public static PlayerSettings Get(ulong steamId)
        {
            var settings = Cache.GetOrAdd(steamId, Load);
            if (!settings.UsesDefaults)
                return settings;

            // The default set is swapped in whole when the config changes, so a reference check spots an admin's edit
            // without allocating on the drill path. TryUpdate keeps a concurrent Update from being overwritten.
            var defaults = OreRules.GetDefaults(OreYeeterCore.Config);
            if (ReferenceEquals(settings.Ores, defaults))
                return settings;

            var current = new PlayerSettings(defaults, settings.AutoDrills, true);
            return Cache.TryUpdate(steamId, current, settings) ? current : Get(steamId);
        }

        /// <summary>
        /// The player's settings after checking their file, for commands that decide from them: another server sharing
        /// the folder may have written it since the last Refresh.
        /// </summary>
        public static PlayerSettings GetChecked(ulong steamId)
        {
            lock (WriteLock)
            {
                try
                {
                    Reload(steamId);
                }
                catch (Exception e)
                {
                    OreYeeterCore.Host.LogError(e, $"Could not check {FilePath(steamId)}, using the list in memory");
                }
                return Get(steamId);
            }
        }

        /// <summary>Applies a change, updating memory first and then the file.</summary>
        public static PlayerSettings Update(ulong steamId, Func<PlayerSettings, PlayerSettings> change)
        {
            lock (WriteLock)
            {
                // Changing stale settings would undo what another server wrote
                var updated = change(GetChecked(steamId));
                Cache[steamId] = updated;
                Save(steamId, updated);
                return updated;
            }
        }

        /// <summary>
        /// Reloads the cached players whose file another process has written since. Only needed when several servers
        /// share the storage folder, like the nodes of a cluster. A player's file has one writer at a time: the server
        /// the player plays on. Runs on a pool thread, so slow or hung storage cannot stall the game thread, and a
        /// call is skipped while the previous one is still running.
        /// </summary>
        public static void Refresh()
        {
            if (Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
                Task.Run(RefreshFiles);
        }

        private static void RefreshFiles()
        {
            try
            {
                foreach (ulong steamId in Cache.Keys)
                    Reload(steamId);
            }
            catch (Exception e)
            {
                OreYeeterCore.Host.LogError(e, "Could not check the player files, keeping the lists in memory");
            }
            finally
            {
                Volatile.Write(ref _refreshing, 0);
            }
        }

        // Replaces the cached settings when the file changed since they were read. Throws when the storage fails: the
        // cached settings stay and the next check tries again.
        private static void Reload(ulong steamId)
        {
            if (!Cache.ContainsKey(steamId))
                return;

            FileTimes.TryGetValue(steamId, out DateTime known);
            string file = FilePath(steamId);
            // Before the read: a write in between leaves an older time, and the next check reads the file again
            DateTime written = File.GetLastWriteTimeUtc(file);
            if (written == known)
                return;

            // The folder gone as well means a lost mount, not a player's deleted list
            if (!File.Exists(file) && !Directory.Exists(Folder))
                return;

            PlayerSettings settings;
            try
            {
                settings = Read(file);
            }
            catch (Exception e) when (!IsStorageError(e))
            {
                // A broken file: not read again until it changes
                FileTimes[steamId] = written;
                OreYeeterCore.Host.LogError(e, $"Could not read {file}, keeping the list in memory");
                return;
            }

            lock (WriteLock)
            {
                // A write on this server while the file was being read is newer
                FileTimes.TryGetValue(steamId, out DateTime current);
                if (current != known)
                    return;

                Cache[steamId] = settings;
                FileTimes[steamId] = written;
            }
        }

        private static PlayerSettings Load(ulong steamId)
        {
            string file = FilePath(steamId);
            DateTime written = default;
            try
            {
                // Before the read: a write in between leaves an older time, and the next check reads the file again
                written = File.GetLastWriteTimeUtc(file);
                var settings = Read(file);
                FileTimes[steamId] = written;
                return settings;
            }
            catch (Exception e)
            {
                // A broken file is treated as the defaults for this session; it is rewritten when the player changes something.
                // After a storage error no time is kept, so the next check reads the file again.
                if (!IsStorageError(e))
                    FileTimes[steamId] = written;
                OreYeeterCore.Host.LogError(e, $"Could not read {file}, using defaults");
                return Defaults();
            }
        }

        private static PlayerSettings Read(string file)
        {
            if (!File.Exists(file))
                return Defaults();

            // The share mode keeps this read from failing the File.Replace of a concurrent Save on Windows
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
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

        private static PlayerSettings Defaults() => new PlayerSettings(OreRules.GetDefaults(OreYeeterCore.Config), false, true);

        private static bool IsStorageError(Exception e) => e is IOException || e is UnauthorizedAccessException;

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
                FileTimes[steamId] = File.GetLastWriteTimeUtc(file);
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
