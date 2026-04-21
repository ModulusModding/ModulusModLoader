using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using ModulusModLoader.Metadata;

namespace ModulusModLoader;

internal static class ModFolderOrdering
{
    private static readonly string[] Empty = Array.Empty<string>();

    internal static List<ModFolderDescriptor> SortByAbout(List<ModFolderDescriptor> descriptors, ManualLogSource log)
    {
        if (descriptors.Count <= 1)
            return descriptors;

        var ids = descriptors
            .Select(d => d.EffectiveModId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var duplicateIds = descriptors
            .GroupBy(d => d.EffectiveModId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var dup in duplicateIds)
            log.LogWarning($"Multiple mod folders share ModID \"{dup}\"; load order may be ambiguous.");

        var depDict = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
            depDict[id] = new List<string>();

        void AddDep(string dependentModId, string prerequisiteModId)
        {
            if (string.IsNullOrEmpty(dependentModId) || string.IsNullOrEmpty(prerequisiteModId))
                return;
            if (string.Equals(dependentModId, prerequisiteModId, StringComparison.OrdinalIgnoreCase))
                return;
            if (!depDict.ContainsKey(dependentModId) || !depDict.ContainsKey(prerequisiteModId))
                return;
            if (!depDict[dependentModId].Contains(prerequisiteModId, StringComparer.OrdinalIgnoreCase))
                depDict[dependentModId].Add(prerequisiteModId);
        }

        foreach (var d in descriptors)
        {
            string self = d.EffectiveModId;
            ModAbout a = d.About;

            if (a.DependsOn != null)
            {
                foreach (var r in a.DependsOn.Where(x => x.IsValid))
                {
                    string? other = ResolveRefModId(r, descriptors);
                    if (other != null)
                        AddDep(self, other);
                    else if (!string.IsNullOrEmpty(r.ModID))
                        log.LogWarning($"[{self}] DependsOn references unknown ModID \"{r.ModID}\".");
                }
            }

            if (a.OrderBefore != null)
            {
                foreach (var r in a.OrderBefore.Where(x => x.IsValid))
                {
                    string? other = ResolveRefModId(r, descriptors);
                    if (other != null)
                        AddDep(other, self);
                    else if (!string.IsNullOrEmpty(r.ModID))
                        log.LogWarning($"[{self}] OrderBefore references unknown ModID \"{r.ModID}\".");
                }
            }

            if (a.OrderAfter != null)
            {
                foreach (var r in a.OrderAfter.Where(x => x.IsValid))
                {
                    string? other = ResolveRefModId(r, descriptors);
                    if (other != null)
                        AddDep(self, other);
                    else if (!string.IsNullOrEmpty(r.ModID))
                        log.LogWarning($"[{self}] OrderAfter references unknown ModID \"{r.ModID}\".");
                }
            }
        }

        List<string> sortedIds;
        try
        {
            sortedIds = Utility.TopologicalSort(depDict.Keys, id =>
                    depDict.TryGetValue(id, out var list) ? list : Empty).ToList();
        }
        catch (Exception ex)
        {
            log.LogWarning($"Mod order has a cycle or conflict; falling back to folder name order. ({ex.Message})");
            return descriptors.OrderBy(d => d.FolderName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sortedIds.Count; i++)
            rank[sortedIds[i]] = i;

        return descriptors
            .OrderBy(d => rank.TryGetValue(d.EffectiveModId, out var r) ? r : int.MaxValue)
            .ThenBy(d => d.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ResolveRefModId(ModReference r, List<ModFolderDescriptor> descriptors)
    {
        if (!string.IsNullOrEmpty(r.ModID))
        {
            foreach (var d in descriptors)
            {
                if (string.Equals(d.EffectiveModId, r.ModID, StringComparison.OrdinalIgnoreCase))
                    return d.EffectiveModId;
            }
        }

        return null;
    }
}
