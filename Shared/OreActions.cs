using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.Game.Multiplayer;
using VRage.Game;

namespace OreYeeter
{
    /// <summary>
    /// The behaviour behind every "!ore" command. The Torch and the Magnetar command classes are thin wrappers that
    /// only declare the command, check the caller and send the returned text back, so both loaders behave the same.
    /// </summary>
    public static class OreActions
    {
        private static readonly ConcurrentDictionary<long, DateTime> CleanCooldowns = new ConcurrentDictionary<long, DateTime>();

        private static IOreConfig Config => OreYeeterCore.Config;

        // ---------------------------------------------------------------- player

        public static string List(ulong steamId) => Summary(steamId);

        public static string Add(ulong steamId, string ore)
        {
            string subtype = FindOre(OreRules.ToOreSubtype(ore));
            if (subtype == null)
                return $"'{ore}' is not a known ore. Available: " + string.Join(", ", OreSubtypes());

            if (PlayerOreStore.Get(steamId).Ores.Contains(subtype))
                return $"{subtype} is already on your list. " + Summary(steamId);

            PlayerOreStore.Update(steamId, s => new PlayerSettings(new HashSet<string>(s.Ores, StringComparer.OrdinalIgnoreCase) { subtype }, s.AutoDrills));
            return $"Added {subtype}. " + Summary(steamId);
        }

        public static string Remove(ulong steamId, string ore)
        {
            string subtype = OreRules.ToOreSubtype(ore);
            if (!PlayerOreStore.Get(steamId).Ores.Contains(subtype))
                return $"{subtype} is not on your list. " + Summary(steamId);

            PlayerOreStore.Update(steamId, s => new PlayerSettings(
                new HashSet<string>(s.Ores.Where(x => !string.Equals(x, subtype, StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase), s.AutoDrills));

            string message = $"Removed {subtype}. " + Summary(steamId);
            if (OreRules.GetMaster(Config).Contains(subtype))
                message += $" {subtype} is on the server list, so it is still yeeted.";
            return message;
        }

        public static string ToggleAuto(ulong steamId)
        {
            var updated = PlayerOreStore.Update(steamId, s => new PlayerSettings(s.Ores, !s.AutoDrills));
            return updated.AutoDrills
                ? "All your ship drills now yeet ores on your list."
                : $"Only drills with '{Config.DrillNameTag}' in their name yeet now.";
        }

        /// <summary>Removes listed ores already stored in the containers of the grid the player sits in or looks at.</summary>
        public static string Clean(ulong steamId)
        {
            if (!Config.Enabled)
                return "OreYeeter is disabled.";

            if (!MySession.Static.Players.TryGetPlayerBySteamId(steamId, out MyPlayer player) || player == null)
                return "Player only command.";

            long playerId = player.Identity.IdentityId;
            if (Config.CleanCooldownSeconds > 0 && CleanCooldowns.TryGetValue(playerId, out DateTime until) && until > DateTime.UtcNow)
                return $"On cooldown for {(int)(until - DateTime.UtcNow).TotalSeconds} more seconds.";

            // Mechanical group = rotor/piston subgrids included, connector docked ships are NOT.
            List<MyCubeGrid> grids;
            if (player.Controller?.ControlledEntity is MyCockpit cockpit)
                grids = MyCubeGridGroups.Static.Mechanical.GetGroupNodes(cockpit.CubeGrid);
            else
                grids = GridUtils.FindLookedAtMechanicalGroup(player.Character);

            if (grids == null || grids.Count == 0)
                return "Sit in a cockpit or look at a grid.";

            var settings = PlayerOreStore.Get(steamId);
            int removed = 0;
            bool skipped = false;
            foreach (var grid in grids)
            {
                foreach (MyCubeBlock block in grid.GetFatBlocks())
                {
                    if (block == null || !block.HasInventory)
                        continue;
                    if (!CanClean(block, playerId))
                    {
                        skipped = true;
                        continue;
                    }

                    var inventory = block.GetInventory();
                    var items = inventory?.GetItems();
                    if (items == null)
                        continue;

                    // Backwards: GetItems() is the live list, removing while moving forwards would skip items.
                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        var content = items[i].Content;
                        if (content.TypeId == typeof(MyObjectBuilder_Ore) && OreRules.ShouldYeet(Config, content.SubtypeName, settings))
                        {
                            removed += (int)items[i].Amount;
                            inventory.RemoveItems(items[i].ItemId);
                        }
                    }
                }
            }

            if (removed > 0 && Config.CleanCooldownSeconds > 0)
                CleanCooldowns[playerId] = DateTime.UtcNow.AddSeconds(Config.CleanCooldownSeconds);

            return $"Yeeted {removed:n0} ore." + (skipped ? " Skipped containers you don't own." : "");
        }

        // ---------------------------------------------------------------- admin

        public static string MasterList() => "Server ore list: " + Join(OreRules.GetMaster(Config));

        public static string MasterAdd(string ore)
        {
            string subtype = FindOre(OreRules.ToOreSubtype(ore));
            if (subtype == null)
                return $"'{ore}' is not a known ore. Available: " + string.Join(", ", OreSubtypes());

            SetMaster(OreRules.GetMaster(Config).Concat(new[] { subtype }));
            return $"Added {subtype}. " + MasterList();
        }

        public static string MasterRemove(string ore)
        {
            string subtype = OreRules.ToOreSubtype(ore);
            var master = OreRules.GetMaster(Config);
            if (!master.Contains(subtype))
                return $"{subtype} is not on the server list. " + MasterList();

            SetMaster(master.Where(x => !string.Equals(x, subtype, StringComparison.OrdinalIgnoreCase)));
            return $"Removed {subtype}. " + MasterList();
        }

        // ---------------------------------------------------------------- helpers

        private static void SetMaster(IEnumerable<string> ores)
        {
            Config.MasterOres = string.Join(",", ores.Distinct(StringComparer.OrdinalIgnoreCase));
            OreYeeterCore.Host.SaveConfig();
        }

        private static string Summary(ulong steamId)
        {
            var s = PlayerOreStore.Get(steamId);
            return $"Server list: {Join(OreRules.GetMaster(Config))} | Your list: {Join(s.Ores)} | " +
                   (s.AutoDrills ? "all your ship drills yeet" : $"drills named '{Config.DrillNameTag}' yeet");
        }

        private static string Join(IEnumerable<string> ores)
        {
            var list = ores.OrderBy(x => x).ToList();
            return list.Count == 0 ? "none" : string.Join(", ", list);
        }

        // Owner, or a member of the owner's faction. Blocks owned by "nobody" (0) are never touched.
        private static bool CanClean(MyCubeBlock block, long playerId)
        {
            long owner = block.OwnerId;
            if (owner == 0)
                return false;
            if (owner == playerId)
                return true;
            var faction = MySession.Static.Factions.TryGetPlayerFaction(playerId);
            return faction != null && faction == MySession.Static.Factions.TryGetPlayerFaction(owner);
        }

        // Every MyObjectBuilder_Ore subtype currently defined (vanilla + mods)
        private static IEnumerable<string> OreSubtypes()
        {
            return MyDefinitionManager.Static.GetPhysicalItemDefinitions()
                .Where(d => d.Id.TypeId == typeof(MyObjectBuilder_Ore))
                .Select(d => d.Id.SubtypeName);
        }

        private static string FindOre(string subtype)
        {
            return OreSubtypes().FirstOrDefault(x => string.Equals(x, subtype, StringComparison.OrdinalIgnoreCase));
        }
    }
}
