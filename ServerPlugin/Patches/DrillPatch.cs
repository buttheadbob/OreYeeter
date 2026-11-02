using HarmonyLib;
using Sandbox.Game.Weapons;
using VRage.Game;

namespace OreYeeter.MagnetarHost.Patches
{
    /// <summary>
    /// Harmony prefix on MyDrillBase.TryHarvestOreMaterial (Magnetar). The Torch build patches the same method
    /// with Torch's own patch manager. The decision lives in the shared DrillLogic.
    /// Returning false skips the original, so the ore is never created.
    /// Harmony binds the parameters by name: they must match the original's names and types.
    /// </summary>
    [HarmonyPatch(typeof(MyDrillBase), "TryHarvestOreMaterial")]
    public static class DrillPatch
    {
        // ReSharper disable once UnusedMember.Local
        [HarmonyPrefix]
        private static bool Prefix(MyDrillBase __instance, MyVoxelMaterialDefinition material, int removedAmount)
        {
            return DrillLogic.ShouldHarvest(__instance, material, removedAmount);
        }
    }
}
