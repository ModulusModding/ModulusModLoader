using System.Collections.Generic;

namespace ModulusModLoader;

internal static class ModLoadReport
{
    internal static int ModFoldersDiscovered { get; private set; }
    internal static int ModFoldersActive { get; private set; }
    internal static int PluginsLoaded { get; private set; }
    internal static bool HasCompleted { get; private set; }

    /// <summary>Display lines for each successfully loaded user-mod plugin.</summary>
    internal static IReadOnlyList<string> LoadedPluginLines { get; private set; } =
        System.Array.Empty<string>();

    internal static void Publish(
        int modFoldersDiscovered,
        int modFoldersActive,
        int pluginsLoaded,
        IReadOnlyList<string> loadedPluginLines)
    {
        ModFoldersDiscovered = modFoldersDiscovered;
        ModFoldersActive = modFoldersActive;
        PluginsLoaded = pluginsLoaded;
        LoadedPluginLines = loadedPluginLines;
        HasCompleted = true;
    }
}
