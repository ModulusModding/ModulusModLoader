using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Newtonsoft.Json;

namespace ModulusModLoader;

/// <summary>
/// Persists per-mod enable flags to <c>Documents\My Games\Modulus\mod_registry.json</c>.
/// Disabled mods are skipped during the next game launch (not unloaded at runtime).
/// </summary>
internal static class ModRegistry
{
    private static readonly Dictionary<string, bool> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;
    private static List<string> _savedLoadOrder = new();

    private static string RegistryPath => ModPaths.GetModRegistryPath();

    internal static void EnsureLoaded(ManualLogSource? log)
    {
        if (_loaded) return;
        _loaded = true;
        Entries.Clear();
        try
        {
            if (!File.Exists(RegistryPath))
                return;

            string json = File.ReadAllText(RegistryPath);
            var data = JsonConvert.DeserializeObject<ModRegistryFile>(json);
            if (data?.Entries == null) return;
            foreach (var e in data.Entries)
            {
                if (!string.IsNullOrWhiteSpace(e.ModId))
                    Entries[e.ModId.Trim()] = e.Enabled;
            }
            if (data.LoadOrder != null)
                _savedLoadOrder = new List<string>(data.LoadOrder);
        }
        catch (Exception ex)
        {
            log?.LogWarning($"Could not read mod registry ({RegistryPath}): {ex.Message}");
        }
    }

    internal static void Save(ManualLogSource? log)
    {
        try
        {
            string? dir = Path.GetDirectoryName(RegistryPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var list = Entries
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new ModRegistryEntry { ModId = kv.Key, Enabled = kv.Value })
                .ToList();
            var file = new ModRegistryFile { Entries = list, LoadOrder = new List<string>(_savedLoadOrder) };
            File.WriteAllText(RegistryPath, JsonConvert.SerializeObject(file, Formatting.Indented));
        }
        catch (Exception ex)
        {
            log?.LogError($"Could not save mod registry: {ex.Message}");
        }
    }

    /// <summary>Merges discovered mod IDs: unknown mods default to enabled.</summary>
    internal static void MergeFromDiscovery(IEnumerable<string> modIds)
    {
        foreach (string id in modIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!Entries.ContainsKey(id))
                Entries[id] = true;
        }
    }

    internal static bool IsEnabled(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId)) return true;
        return !Entries.TryGetValue(modId, out bool en) || en;
    }

    internal static void SetEnabled(string modId, bool enabled, ManualLogSource? log)
    {
        EnsureLoaded(log);
        Entries[modId] = enabled;
        Save(log);
    }

    internal static IReadOnlyDictionary<string, bool> Snapshot() =>
        new Dictionary<string, bool>(Entries, StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the user's saved drag-drop load order, or null if none saved yet.</summary>
    internal static List<string>? GetSavedLoadOrder() =>
        _savedLoadOrder.Count > 0 ? new List<string>(_savedLoadOrder) : null;

    /// <summary>Persists the new load order (called after a drag-drop reorder).</summary>
    internal static void SetLoadOrder(List<string> order, ManualLogSource? log)
    {
        _savedLoadOrder = new List<string>(order);
        Save(log);
    }

    private sealed class ModRegistryFile
    {
        [JsonProperty("entries")]
        public List<ModRegistryEntry> Entries { get; set; } = new();

        [JsonProperty("loadOrder")]
        public List<string> LoadOrder { get; set; } = new();
    }

    private sealed class ModRegistryEntry
    {
        [JsonProperty("modId")]
        public string ModId { get; set; } = "";

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }
}
