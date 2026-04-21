using System.Collections;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModulusModLoader.Patches;

/// <summary>
/// Hooks <c>SplashScreens.Start</c> to load user mods and optionally skip splash screens.
/// </summary>
[HarmonyPatch(typeof(SplashScreens), "Start")]
internal static class SplashScreensStartPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SplashScreens __instance)
    {
        if (!LoaderConfig.SkipSplashScreens.Value)
            return true;

        var log = ModulusModLoaderPlugin.Log;
        if (log != null)
            log.LogDebug("SplashScreens.Start — skipping splash sequence (Startup.SkipSplashScreens).");

        __instance.StartCoroutine(CoSkipSplashWithMods(__instance, log));
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(SplashScreens __instance)
    {
        if (LoaderConfig.SkipSplashScreens.Value)
            return;

        var log = ModulusModLoaderPlugin.Log;
        if (log == null) return;

        log.LogDebug("SplashScreens.Start postfix — loading user mods…");
        __instance.StartCoroutine(CoLoadModsThenRaiseGameStarted(log));
    }

    private static IEnumerator CoSkipSplashWithMods(SplashScreens __instance, ManualLogSource? log)
    {
        if (log != null)
        {
            ModLoadProgress.Begin();
            try
            {
                yield return ModsRootPluginLoader.LoadAllCoroutine(log);
                ModGameLifecycle.RaiseGameStarted();
            }
            finally
            {
                ModLoadProgress.End();
            }
        }

        yield return SkipToMainMenu(__instance);
    }

    private static IEnumerator CoLoadModsThenRaiseGameStarted(ManualLogSource log)
    {
        ModLoadProgress.Begin();
        try
        {
            yield return ModsRootPluginLoader.LoadAllCoroutine(log);
        }
        finally
        {
            ModLoadProgress.End();
        }

        ModGameLifecycle.RaiseGameStarted();
    }

    private static IEnumerator SkipToMainMenu(SplashScreens __instance)
    {
        GameObject? loading = Traverse.Create(__instance).Field<GameObject>("_loadingScreen").Value;
        if (loading != null)
            loading.SetActive(true);

        string scene = Traverse.Create(__instance).Field<string>("_sceneToLoad").Value;
        if (string.IsNullOrEmpty(scene))
            scene = "StartScreen";

        yield return null;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene);
        while (!asyncLoad.isDone)
            yield return null;
    }
}
