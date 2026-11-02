using System;
using System.Reflection;
using Sandbox.Game.Weapons;
using Torch.Managers.PatchManager;
using VRage.Game;

namespace OreYeeter.TorchHost.Patches
{
    /// <summary>
    /// Torch native patch (no Harmony). Torch's PatchManager finds this class through [PatchShim] and calls Patch().
    /// The prefix runs before MyDrillBase.TryHarvestOreMaterial and returning false skips it, so the ore is never created.
    /// The decision itself lives in the shared DrillLogic.
    /// </summary>
    [PatchShim]
    public static class DrillPatch
    {
        private static readonly MethodInfo Target =
            typeof(MyDrillBase).GetMethod("TryHarvestOreMaterial", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("OreYeeter: could not find MyDrillBase.TryHarvestOreMaterial, the game has probably changed");

        private static readonly MethodInfo Prefix =
            typeof(DrillPatch).GetMethod(nameof(TryHarvestPrefix), BindingFlags.Static | BindingFlags.Public)
            ?? throw new Exception("OreYeeter: could not find the prefix method");

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(Target).Prefixes.Add(Prefix);
            OreYeeterPlugin.Log.Info("Patched MyDrillBase.TryHarvestOreMaterial");
        }

        // Torch binds prefix parameters to the original's by NAME only (there is no type check), so every parameter
        // used here must be named exactly like the original's and have its exact type. Unused ones are left out.
        public static bool TryHarvestPrefix(MyDrillBase __instance, MyVoxelMaterialDefinition material, int removedAmount)
        {
            return DrillLogic.ShouldHarvest(__instance, material, removedAmount);
        }
    }
}
