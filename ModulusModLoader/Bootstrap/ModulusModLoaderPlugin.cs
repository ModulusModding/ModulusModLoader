using System;
using System.Collections;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ModulusModLoader;

[BepInPlugin(LoaderPluginInfo.Guid, LoaderPluginInfo.Name, LoaderPluginInfo.Version)]
public class ModulusModLoaderPlugin : BaseUnityPlugin
{
    internal static ManualLogSource? Log { get; private set; }

    private Harmony? _harmony;

    private void Awake()
    {
        Log = Logger;
        LoaderConfig.Bind(Config);
        string root = ModPaths.GetUserModsRoot(Log);
        Log.LogInfo($"{LoaderPluginInfo.Name} v{LoaderPluginInfo.Version} | User mods folder: {root}");

        try
        {
            _harmony = new Harmony(LoaderPluginInfo.Guid);
            _harmony.PatchAll(typeof(ModulusModLoaderPlugin).Assembly);
            Log.LogDebug("Harmony PatchAll finished.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Harmony setup failed: {ex}");
        }
    }

    private void Start()
    {
        StartCoroutine(RunLoaderUpdateCheck());
    }

    private IEnumerator RunLoaderUpdateCheck()
    {
        yield return null;
        yield return LoaderSelfUpdate.CoRun();
    }

    private void OnDestroy()
    {
        try
        {
            ModAssetBundles.UnloadAll();
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Asset bundle unload: {ex.Message}");
        }

        try
        {
            _harmony?.UnpatchSelf();
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Unpatch: {ex.Message}");
        }
    }
}
