using System.Collections;
using HarmonyLib;
using Utils.SceneHandling;

namespace ModulusModLoader.Patches;

/// <summary>
/// Hooks <c>SceneHandler.Start</c> (coroutine) to fire <see cref="ModGameLifecycle.SceneLoaded"/>
/// after each scene finishes loading.
/// </summary>
[HarmonyPatch(typeof(SceneHandler), "Start")]
internal static class SceneHandlerStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(SceneHandler __instance, ref IEnumerator __result)
    {
        __result = WrapCoroutine(__result);
    }

    private static IEnumerator WrapCoroutine(IEnumerator original)
    {
        while (original.MoveNext())
            yield return original.Current;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        ModulusModLoaderPlugin.Log?.LogDebug($"SceneHandler.Start finished — scene: {sceneName}");
        ModGameLifecycle.RaiseSceneLoaded(sceneName);
    }
}
