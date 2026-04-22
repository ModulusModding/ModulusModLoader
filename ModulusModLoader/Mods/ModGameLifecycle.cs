using System;

namespace ModulusModLoader;

/// <summary>
/// Public lifecycle events that gameplay mods can subscribe to.
/// All events fire on the Unity main thread.
/// </summary>
public static class ModGameLifecycle
{
    /// <summary>Fires once after the splash screen starts and user mods are loaded.</summary>
    public static event Action? GameStarted;

    /// <summary>Fires every time a scene finishes loading (receives the scene name).</summary>
    public static event Action<string>? SceneLoaded;

    /// <summary>Fires after the factory has finished loading a save.</summary>
    public static event Action? FactoryLoaded;

    /// <summary>Fires when the factory/level is being cleared (before a new load or exit).</summary>
    public static event Action? FactoryClearing;

    internal static void RaiseGameStarted()
    {
        try { GameStarted?.Invoke(); }
        catch (Exception ex) { ModulusModLoaderPlugin.Log?.LogError($"ModGameLifecycle.GameStarted: {ex}"); }
    }

    internal static void RaiseSceneLoaded(string sceneName)
    {
        try { SceneLoaded?.Invoke(sceneName); }
        catch (Exception ex) { ModulusModLoaderPlugin.Log?.LogError($"ModGameLifecycle.SceneLoaded({sceneName}): {ex}"); }
    }

    internal static void RaiseFactoryLoaded()
    {
        try { FactoryLoaded?.Invoke(); }
        catch (Exception ex) { ModulusModLoaderPlugin.Log?.LogError($"ModGameLifecycle.FactoryLoaded: {ex}"); }
    }

    internal static void RaiseFactoryClearing()
    {
        try { FactoryClearing?.Invoke(); }
        catch (Exception ex) { ModulusModLoaderPlugin.Log?.LogError($"ModGameLifecycle.FactoryClearing: {ex}"); }
    }
}
