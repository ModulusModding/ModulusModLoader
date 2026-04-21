using System;
using System.IO;
using BepInEx.Logging;

namespace ModulusModLoader;

internal static class ModPaths
{
    internal static string GetUserModsRoot(ManualLogSource? log = null)
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(docs))
            docs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        string root = Path.Combine(docs, "My Games", "Modulus", "mods");
        root = Path.GetFullPath(root);

        try
        {
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
                log?.LogInfo($"Created user mods root: {root}");
            }
        }
        catch (Exception ex)
        {
            log?.LogError($"Could not create user mods root {root}: {ex.Message}");
        }

        return root;
    }

    /// <summary>JSON file next to the mods folder: per-mod enable/disable.</summary>
    internal static string GetModRegistryPath()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(docs))
            docs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        return Path.GetFullPath(Path.Combine(docs, "My Games", "Modulus", "mod_registry.json"));
    }
}
