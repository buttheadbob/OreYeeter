using System;
using Sandbox.Game.Weapons;
using VRage.Game;

namespace OreYeeter
{
    /// <summary>The decision made by the TryHarvestOreMaterial prefix. Each host only declares the patch itself.</summary>
    public static class DrillLogic
    {
        /// <summary>
        /// Called for every voxel material on every drill tick, so it must stay allocation free.
        /// Returns true to let the game harvest normally, false to void the ore (it is never created).
        /// </summary>
        public static bool ShouldHarvest(MyDrillBase drillBase, MyVoxelMaterialDefinition material)
        {
            var config = OreYeeterCore.Config;
            if (config == null || !config.Enabled)
                return true;

            string ore = material?.MinedOre;
            if (string.IsNullOrEmpty(ore))
                return true;

            // Only ship drills are affected, hand drills have a character as owner
            if (!(drillBase.OutputInventory?.Owner?.GetBaseEntity() is MyShipDrill drill))
                return true;

            var settings = PlayerOreStore.GetForIdentity(drill.OwnerId);
            string tag = config.DrillNameTag;
            bool tagged = !string.IsNullOrEmpty(tag) && drill.DisplayNameText != null
                          && drill.DisplayNameText.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!tagged && settings?.AutoDrills != true)
                return true;

            return !OreRules.ShouldYeet(config, ore, settings);
        }
    }
}
