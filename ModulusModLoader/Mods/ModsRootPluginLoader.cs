using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Mono.Cecil;
using UnityEngine;

namespace ModulusModLoader;

internal static class ModsRootPluginLoader
{
    private static bool _loadAllCompleted;
    private static readonly string[] EmptyDependencies = Array.Empty<string>();
    private static readonly string BepInExAssemblyName = typeof(BepInPlugin).Assembly.GetName().Name!;
    private static readonly Version BepInExVersion = typeof(Chainloader).Assembly.GetName().Version!;

    internal static IEnumerator LoadAllCoroutine(ManualLogSource log)
    {
        if (_loadAllCompleted)
            yield break;

        int discovered = 0;
        int active = 0;
        int pluginsLoaded = 0;
        var loadedPluginLines = new List<string>();
        try
        {
            List<ModFolderDescriptor>? descriptors = PrepareDescriptorsForLoad(log, ref discovered, ref active);
            if (descriptors == null)
                yield break;

            ModLoadProgress.Set("Loading mods", $"{descriptors.Count} folder(s) in load order");
            yield return null;

            foreach (object? step in ModAssetBundles.LoadForModsCoroutine(descriptors, log))
                yield return step;

            LoadAfterBundles(descriptors, log, ref pluginsLoaded, loadedPluginLines);
        }
        finally
        {
            ModLoadReport.Publish(discovered, active, pluginsLoaded, loadedPluginLines);
            _loadAllCompleted = true;
        }
    }

    private static List<ModFolderDescriptor>? PrepareDescriptorsForLoad(
        ManualLogSource log,
        ref int modFoldersDiscovered,
        ref int modFoldersActive)
    {
        LoaderStatusBuffer.Clear();
        ModRegistry.EnsureLoaded(log);

        string modsRoot = ModPaths.GetUserModsRoot(log);
        LoaderStatusBuffer.Add($"Mods: {modsRoot}");

        var descriptors = ModFolderDiscovery.Discover(modsRoot, log);
        modFoldersDiscovered = descriptors.Count;

        if (descriptors.Count == 0)
        {
            log.LogInfo("User mods: no folders with About/About.xml found.");
            LoaderStatusBuffer.Add("No About/About.xml mod folders.");
            return null;
        }

        ModRegistry.MergeFromDiscovery(descriptors.Select(d => d.EffectiveModId));

        foreach (var d in descriptors.Where(x => !ModRegistry.IsEnabled(x.EffectiveModId)))
        {
            log.LogInfo($"Skipping disabled mod: {d.EffectiveModId}");
            LoaderStatusBuffer.Add($"Off: {d.EffectiveName} ({d.EffectiveModId})");
        }

        descriptors = descriptors.Where(d => ModRegistry.IsEnabled(d.EffectiveModId)).ToList();
        modFoldersActive = descriptors.Count;

        if (descriptors.Count == 0)
        {
            log.LogInfo("User mods: all discovered mods are disabled in mod_registry.json.");
            LoaderStatusBuffer.Add("All mods disabled — enable in main menu Mods.");
            return null;
        }

        if (LoaderConfig.VerboseLog.Value)
        {
            foreach (var d in descriptors)
            {
                string aboutFlag = d.AboutInvalid ? "About.xml invalid or partial" : "About.xml OK";
                log.LogDebug(
                    $"  {d.EffectiveModId} | {d.EffectiveName} | v{d.About.Version ?? "?"} | {aboutFlag} | {d.RootPath}");
            }
        }

        descriptors = ModFolderOrdering.SortByAbout(descriptors, log);
        log.LogInfo($"Mod load order: {string.Join(" -> ", descriptors.Select(x => x.EffectiveModId))}");
        foreach (var d in descriptors)
            LoaderStatusBuffer.Add($"{d.EffectiveName} ({d.EffectiveModId})");

        return descriptors;
    }

    private static void LoadAfterBundles(
        List<ModFolderDescriptor> descriptors,
        ManualLogSource log,
        ref int pluginsLoaded,
        List<string> loadedPluginLines)
    {
        ModLoadProgress.Set("Scanning mod plugins", $"{descriptors.Count} folder(s)");

        var allPluginInfos = new List<PluginInfo>();
        foreach (var mod in descriptors)
        {
            ModLoadProgress.Set("Scanning mod plugins", $"{mod.EffectiveName} ({mod.EffectiveModId})");
            log.LogDebug($"Scanning DLLs: {mod.EffectiveModId} <- {mod.RootPath}");

            string cacheKey = "modulus_mods_" + SanitizeCacheKey(mod.EffectiveModId);
            Dictionary<string, List<PluginInfo>> found;
            try
            {
                found = TypeLoader.FindPluginTypes(mod.RootPath, Chainloader.ToPluginInfo, HasBepinPlugins, cacheKey);
            }
            catch (Exception ex)
            {
                log.LogError($"[{mod.EffectiveModId}] Failed scanning assemblies: {ex.Message}");
                continue;
            }

            foreach (var kv in found)
            {
                foreach (var info in kv.Value)
                {
                    PluginInfoReflection.SetLocation(info, kv.Key);
                    allPluginInfos.Add(info);
                    log.LogDebug(
                        $"  -> Plugin candidate: {info.Metadata.Name} v{info.Metadata.Version} ({info.Metadata.GUID}) @ {kv.Key}");
                }
            }
        }

        if (allPluginInfos.Count == 0)
        {
            log.LogInfo($"User mods: {descriptors.Count} folder(s) listed but no [BepInPlugin] DLLs found.");
            LogFingerprintsIfVerbose(log, descriptors);
            return;
        }

        var pluginsByGuid = DeduplicateByGuid(allPluginInfos, log);
        RemoveAlreadyLoaded(pluginsByGuid, log);

        if (pluginsByGuid.Count == 0)
        {
            log.LogWarning("User mods: every plugin was skipped (same GUID already loaded from BepInEx/plugins).");
            LogFingerprintsIfVerbose(log, descriptors);
            return;
        }

        CheckIncompatibilities(pluginsByGuid, log);
        var loadOrderGuids = ResolveDependencyOrder(pluginsByGuid, log);

        if (loadOrderGuids == null)
            return;

        pluginsLoaded = InstantiatePlugins(loadOrderGuids, pluginsByGuid, log, loadedPluginLines);

        LogFingerprintsIfVerbose(log, descriptors);
        log.LogInfo(
            $"User mods: finished - {descriptors.Count} folder(s), {pluginsLoaded} BepInEx plugin(s) loaded.");
    }

    private static Dictionary<string, PluginInfo> DeduplicateByGuid(List<PluginInfo> allPluginInfos, ManualLogSource log)
    {
        var pluginsByGuid = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in allPluginInfos.GroupBy(i => i.Metadata.GUID, StringComparer.OrdinalIgnoreCase))
        {
            PluginInfo? chosen = null;
            foreach (var info in group.OrderByDescending(x => x.Metadata.Version))
            {
                if (chosen != null)
                {
                    log.LogWarning($"Skipping [{info}] because a newer version exists ({chosen})");
                    continue;
                }
                chosen = info;
            }
            if (chosen != null)
                pluginsByGuid[chosen.Metadata.GUID] = chosen;
        }
        return pluginsByGuid;
    }

    private static void RemoveAlreadyLoaded(Dictionary<string, PluginInfo> pluginsByGuid, ManualLogSource log)
    {
        foreach (var guid in pluginsByGuid.Keys.ToList())
        {
            if (Chainloader.PluginInfos.ContainsKey(guid))
            {
                log.LogWarning($"Skipping [{pluginsByGuid[guid]}] (GUID already loaded from BepInEx/plugins).");
                pluginsByGuid.Remove(guid);
            }
        }
    }

    private static void CheckIncompatibilities(Dictionary<string, PluginInfo> pluginsByGuid, ManualLogSource log)
    {
        foreach (var info in pluginsByGuid.Values.ToList())
        {
            if (!info.Incompatibilities.Any(inc =>
                    Chainloader.PluginInfos.ContainsKey(inc.IncompatibilityGUID)
                    || pluginsByGuid.ContainsKey(inc.IncompatibilityGUID)))
                continue;

            var bad = info.Incompatibilities.Select(x => x.IncompatibilityGUID)
                .Where(x => Chainloader.PluginInfos.ContainsKey(x) || pluginsByGuid.ContainsKey(x)).ToArray();
            log.LogError($"Could not load [{info}] because it is incompatible with: {string.Join(", ", bad)}");
            Chainloader.DependencyErrors.Add($"User mod: incompatible {info.Metadata.GUID}");
            pluginsByGuid.Remove(info.Metadata.GUID);
        }
    }

    private static List<string>? ResolveDependencyOrder(Dictionary<string, PluginInfo> pluginsByGuid, ManualLogSource log)
    {
        var dependencyDict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in pluginsByGuid)
            dependencyDict[kv.Key] = kv.Value.Dependencies.Select(d => d.DependencyGUID);

        List<string> sortedGuids;
        try
        {
            sortedGuids = Utility.TopologicalSort(dependencyDict.Keys, x =>
                    dependencyDict.TryGetValue(x, out var deps) ? deps : EmptyDependencies)
                .ToList();
        }
        catch (Exception ex)
        {
            log.LogError($"Mod dependency sort failed: {ex.Message}");
            return null;
        }

        return sortedGuids.Where(g => pluginsByGuid.ContainsKey(g)).ToList();
    }

    private static int InstantiatePlugins(
        List<string> loadOrderGuids,
        Dictionary<string, PluginInfo> pluginsByGuid,
        ManualLogSource log,
        List<string> loadedPluginLines)
    {
        var loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        var invalidPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedPlugins = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in Chainloader.PluginInfos)
            processedPlugins[kv.Key] = kv.Value.Metadata.Version;

        GameObject? manager = Chainloader.ManagerObject;
        if (manager == null)
        {
            log.LogError("Chainloader.ManagerObject is null; cannot attach plugins from the mods root.");
            return 0;
        }

        FieldInfo? pluginListField = typeof(Chainloader).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Static);
        IList? pluginList = pluginListField?.GetValue(null) as IList;

        int loaded = 0;
        int pluginTotal = loadOrderGuids.Count;
        int pluginOrdinal = 0;
        foreach (var pluginGuid in loadOrderGuids)
        {
            var pluginInfo = pluginsByGuid[pluginGuid];
            pluginOrdinal++;
            ModLoadProgress.Set($"Starting plugins ({pluginOrdinal}/{pluginTotal})", pluginInfo.Metadata.Name);
            try
            {
                bool dependsOnInvalid = false;
                var missingDeps = new List<BepInDependency>();
                foreach (var dep in pluginInfo.Dependencies)
                {
                    bool isHard = (dep.Flags & BepInDependency.DependencyFlags.HardDependency) != 0;
                    bool depExists = processedPlugins.TryGetValue(dep.DependencyGUID, out var pluginVersion);
                    if (!depExists || pluginVersion < dep.MinimumVersion)
                    {
                        if (isHard) missingDeps.Add(dep);
                        continue;
                    }
                    if (invalidPlugins.Contains(dep.DependencyGUID) && isHard)
                    {
                        dependsOnInvalid = true;
                        break;
                    }
                }

                processedPlugins[pluginGuid] = pluginInfo.Metadata.Version;

                if (dependsOnInvalid)
                {
                    log.LogWarning($"Skipping [{pluginInfo}] because a dependency was not loaded.");
                    continue;
                }

                if (missingDeps.Count != 0)
                {
                    bool IsEmptyVersion(Version v) => v.Major == 0 && v.Minor == 0 && v.Build <= 0 && v.Revision <= 0;
                    string message = $"Could not load [{pluginInfo}] because it has missing dependencies: {
                        string.Join(", ", missingDeps.Select(s =>
                            IsEmptyVersion(s.MinimumVersion)
                                ? s.DependencyGUID
                                : $"{s.DependencyGUID} (v{s.MinimumVersion} or newer)").ToArray())}";
                    Chainloader.DependencyErrors.Add(message);
                    log.LogError(message);
                    invalidPlugins.Add(pluginGuid);
                    continue;
                }

                if (!loadedAssemblies.TryGetValue(pluginInfo.Location, out var ass))
                    loadedAssemblies[pluginInfo.Location] = ass = Assembly.LoadFile(pluginInfo.Location);

                string? typeName = PluginInfoReflection.GetTypeName(pluginInfo);
                Type? pluginType = typeName != null ? ass.GetType(typeName) : null;
                if (pluginType == null)
                {
                    log.LogError($"Type not found [{typeName}] in {pluginInfo.Location}");
                    invalidPlugins.Add(pluginGuid);
                    continue;
                }

                Chainloader.PluginInfos[pluginGuid] = pluginInfo;
                var instance = (BaseUnityPlugin)manager.AddComponent(pluginType);
                PluginInfoReflection.SetInstance(pluginInfo, instance);
                pluginList?.Add(instance);

                string line = $"{pluginInfo.Metadata.Name} ({pluginInfo.Metadata.GUID})";
                log.LogInfo($"Loaded user mod plugin: {line}");
                loadedPluginLines.Add(line);
                LoaderStatusBuffer.Add($"Loaded: {pluginInfo.Metadata.Name}");
                loaded++;
            }
            catch (Exception ex)
            {
                invalidPlugins.Add(pluginGuid);
                Chainloader.PluginInfos.Remove(pluginGuid);
                log.LogError($"Error loading [{pluginInfo}]: {ex.Message}");
                if (ex is ReflectionTypeLoadException re)
                    log.LogDebug(TypeLoader.TypeLoadExceptionToString(re));
                else
                    log.LogDebug(ex);
            }
        }

        return loaded;
    }

    private static void LogFingerprintsIfVerbose(ManualLogSource log, List<ModFolderDescriptor> descriptors)
    {
        if (!LoaderConfig.VerboseLog.Value) return;
        foreach (var mod in descriptors)
        {
            try
            {
                string fp = ComputeModFingerprint(mod);
                log.LogDebug($"Mod fingerprint: {mod.EffectiveModId} v{mod.About.Version ?? "?"} fp={fp}");
            }
            catch (Exception ex)
            {
                log.LogWarning($"Fingerprint for {mod.EffectiveModId}: {ex.Message}");
            }
        }
    }

    private static string ComputeModFingerprint(ModFolderDescriptor mod)
    {
        var sb = new StringBuilder();
        string aboutPath = Path.Combine(mod.RootPath, "About", "About.xml");
        if (File.Exists(aboutPath))
            sb.Append(File.ReadAllText(aboutPath));

        foreach (string dll in Directory.GetFiles(mod.RootPath, "*.dll", SearchOption.AllDirectories)
                     .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}About{Path.DirectorySeparatorChar}",
                         StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var fi = new FileInfo(dll);
            sb.Append('|').Append(fi.FullName).Append(':').Append(fi.Length);
        }

        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        var result = new StringBuilder(16);
        for (var i = 0; i < 8 && i < hash.Length; i++)
            result.AppendFormat("{0:x2}", hash[i]);
        return result.ToString();
    }

    private static string SanitizeCacheKey(string modId)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            modId = modId.Replace(c, '_');
        return modId.Length > 64 ? modId.Substring(0, 64) : modId;
    }

    private static bool HasBepinPlugins(AssemblyDefinition ass)
    {
        if (ass.MainModule.AssemblyReferences.All(r => r.Name != BepInExAssemblyName))
            return false;
        if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(BepInPlugin).FullName))
            return false;
        return true;
    }
}
