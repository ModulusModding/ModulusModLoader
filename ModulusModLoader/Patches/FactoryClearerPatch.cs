using HarmonyLib;
using Logic.Factory;

namespace ModulusModLoader.Patches;

/// <summary>
/// Hooks <c>FactoryClearer.ClearLevel</c> to fire <see cref="ModGameLifecycle.FactoryClearing"/>
/// before the game clears the factory.
/// </summary>
[HarmonyPatch(typeof(FactoryClearer), nameof(FactoryClearer.ClearLevel))]
internal static class FactoryClearerClearLevelPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        ModulusModLoaderPlugin.Log?.LogDebug("FactoryClearer.ClearLevel prefix — raising FactoryClearing.");
        ModGameLifecycle.RaiseFactoryClearing();
    }
}
