using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;

namespace ModulusModLoader;

/// <summary>
/// Discovers and loads Unity asset bundles from user mod folders: recursive globs
/// <c>*.bundle</c>, <c>*.assetbundle</c>, <c>*.assets</c>, merged and de-duplicated by full path.
/// Loads with <see cref="AssetBundle.LoadFromFileAsync"/>; the host coroutine yields until each request completes so the player loop can run.
/// </summary>
public static class ModAssetBundles
{
    private static readonly string[] BundleSearchPatterns = { "*.bundle", "*.assetbundle", "*.assets" };

    private static readonly Dictionary<string, List<string>> DeclaredPathsByModId =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, AssetBundle> LoadedByFullPath =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>All loaded bundles keyed by full normalized file path (case-insensitive).</summary>
    public static IReadOnlyDictionary<string, AssetBundle> LoadedByPath => LoadedByFullPath;

    /// <summary>
    /// Full paths to bundle files discovered under the given mod (by <see cref="Metadata.ModAbout.ModID"/> or folder fallback),
    /// in sorted order. Empty if the mod was not loaded or has no matching files.
    /// </summary>
    public static IReadOnlyList<string> GetDeclaredAssetPaths(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return Array.Empty<string>();

        return DeclaredPathsByModId.TryGetValue(modId.Trim(), out List<string>? list)
            ? list.ToArray()
            : Array.Empty<string>();
    }

    /// <summary>Returns the bundle loaded from this path, or null if not found or not yet loaded.</summary>
    public static AssetBundle? TryGetLoaded(string fullAssetBundlePath)
    {
        if (string.IsNullOrWhiteSpace(fullAssetBundlePath))
            return null;

        string key = Path.GetFullPath(fullAssetBundlePath);
        return LoadedByFullPath.TryGetValue(key, out AssetBundle? b) ? b : null;
    }

    internal static void UnloadAll() => Clear();

    private static void Clear()
    {
        foreach (AssetBundle b in LoadedByFullPath.Values)
        {
            try
            {
                b?.Unload(false);
            }
            catch
            {
                // ignore
            }
        }

        LoadedByFullPath.Clear();
        DeclaredPathsByModId.Clear();
    }

    private static List<string> CollectBundlePaths(string modRoot, ManualLogSource log)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pattern in BundleSearchPatterns)
        {
            try
            {
                foreach (string p in Directory.GetFiles(modRoot, pattern, SearchOption.AllDirectories))
                    set.Add(Path.GetFullPath(p));
            }
            catch (Exception ex)
            {
                log.LogWarning($"Bundle search {pattern} under {modRoot}: {ex.Message}");
            }
        }

        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    internal static IEnumerable LoadForModsCoroutine(IReadOnlyList<ModFolderDescriptor> descriptors, ManualLogSource log)
    {
        Clear();

        int bundleTotal = 0;
        foreach (ModFolderDescriptor m in descriptors)
        {
            if (!Directory.Exists(m.RootPath))
                continue;
            bundleTotal += CollectBundlePaths(m.RootPath, log).Count;
        }

        ModLoadProgress.Set(
            "Asset bundles",
            bundleTotal == 0 ? "None found" : $"{bundleTotal} file(s) to load");
        yield return null;

        int bundleDone = 0;
        foreach (ModFolderDescriptor mod in descriptors)
        {
            string modId = mod.EffectiveModId;
            string modRoot = mod.RootPath;
            if (!Directory.Exists(modRoot))
                continue;

            List<string> paths = CollectBundlePaths(modRoot, log);
            DeclaredPathsByModId[modId] = paths;

            foreach (string path in paths)
            {
                string full = Path.GetFullPath(path);
                if (LoadedByFullPath.ContainsKey(full))
                {
                    log.LogWarning($"Duplicate asset bundle path (skipping second load): {full}");
                    continue;
                }

                if (bundleTotal > 0)
                {
                    ModLoadProgress.Set(
                        $"Asset bundles ({bundleDone + 1}/{bundleTotal})",
                        $"{modId} — {Path.GetFileName(path)}");
                }

                AssetBundleCreateRequest req;
                try
                {
                    req = AssetBundle.LoadFromFileAsync(path);
                }
                catch (Exception ex)
                {
                    log.LogError($"LoadFromFileAsync for {path}: {ex.Message}");
                    continue;
                }

                while (!req.isDone)
                    yield return null;

                AssetBundle? bundle = req.assetBundle;
                if (bundle == null)
                {
                    log.LogError($"AssetBundle load failed: {path}");
                    continue;
                }

                LoadedByFullPath[full] = bundle;
                log.LogInfo($"Loaded asset bundle: {full}");
                bundleDone++;
            }
        }
    }

}
