using System.Collections;
using HarmonyLib;
using Logic.Factory;

namespace ModulusModLoader.Patches;

/// <summary>
/// Hooks <c>FactoryLoader.TryLoadLevel</c> to fire <see cref="ModGameLifecycle.FactoryLoaded"/>
/// after the factory finishes loading a save.
/// </summary>
[HarmonyPatch(typeof(FactoryLoader), nameof(FactoryLoader.TryLoadLevel))]
internal static class FactoryLoaderTryLoadLevelPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerator __result)
    {
        __result = WrapCoroutine(__result);
    }

    private static IEnumerator WrapCoroutine(IEnumerator original)
    {
        while (original.MoveNext())
            yield return original.Current;

        ModulusModLoaderPlugin.Log?.LogDebug("FactoryLoader.TryLoadLevel coroutine finished — raising FactoryLoaded.");
        ModGameLifecycle.RaiseFactoryLoaded();
    }
}
