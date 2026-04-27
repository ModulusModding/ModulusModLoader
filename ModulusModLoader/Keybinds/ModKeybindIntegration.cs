using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Data.SaveData.PersistentSOs;
using Data.UI.Controls;
using HarmonyLib;
using ModulusModLoader.Localization;
using Presentation.UI;
using Presentation.UI.Controls;
using Presentation.UI.Menus.SettingsCategories.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModulusModLoader;

/// <summary>
/// Injects mod keybinds into the vanilla Settings → Controls UI.
///
/// Strategy: do NOT instantiate any UI ourselves. The game's
/// <see cref="SettingsControlsPopulator.Populate"/> iterates
/// <see cref="SettingsRebindRuntimeInfo.AllRebindActions"/> and instantiates a subtitle whenever the
/// group changes plus a rebind row per action. So we only have to:
///   1. Add a real <see cref="InputAction"/> per registration to the game's
///      <see cref="InputActionAsset"/> under map <see cref="MapName"/> so binding-override JSON
///      keys line up with vanilla persistence.
///   2. Push a synthesized <see cref="SettingsRebindAction"/> into the runtime info's internal
///      lists/dictionaries from a postfix on <see cref="SettingsRebindRuntimeInfo.Initialize"/>.
///   3. When the controls tab opens (or registrations change after the menu has been built), tear
///      down the previous clone rows and call <see cref="SettingsControlsPopulator.Populate"/>
///      again so vanilla rebuilds the UI including our injected actions.
/// </summary>
internal static class ModKeybindIntegration
{
    internal const string MapName = "ModulusModLoader";

    private static readonly object LockObj = new();
    private static readonly List<ModKeybindSlot> Slots = new();

    /// <summary>Category key (case-insensitive) → runtime group asset.</summary>
    private static readonly Dictionary<string, SettingsRebindGroup> CategoryGroups =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Runtime group → subtitle text shown in Controls.</summary>
    private static readonly Dictionary<SettingsRebindGroup, string> GroupDisplayTitle =
        new(ReferenceEqualityComparer<SettingsRebindGroup>.Instance);

    internal static IReadOnlyList<ModKeybindRegistration> RegistrationsSnapshot
    {
        get
        {
            lock (LockObj)
                return Slots.Select(s => s.Registration).ToArray();
        }
    }

    internal static InputAction? Register(
        string modId,
        string actionId,
        string displayName,
        string defaultBindingPath,
        string category,
        string? displayNameLocalizationKey)
    {
        ModKeybindSlot slot;
        lock (LockObj)
        {
            Guid bindingGuid = StableBindingGuid(modId, actionId);
            ModKeybindSlot? existing = Slots.FirstOrDefault(s =>
                string.Equals(s.Registration.ModId, modId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.Registration.ActionId, actionId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Registration = new ModKeybindRegistration(
                    modId, actionId, displayName, defaultBindingPath, bindingGuid, category, displayNameLocalizationKey);
                slot = existing;
            }
            else
            {
                slot = new ModKeybindSlot(new ModKeybindRegistration(
                    modId, actionId, displayName, defaultBindingPath, bindingGuid, category, displayNameLocalizationKey));
                Slots.Add(slot);
            }
        }

        string labelForLog = displayName;
        if (!string.IsNullOrEmpty(displayNameLocalizationKey))
            labelForLog = ModL10n.Get(modId, displayNameLocalizationKey!, displayName);
        ModulusModLoaderPlugin.Log?.LogInfo(
            $"ModKeybind.Register: {modId}/{actionId} '{labelForLog}' [{category}] default={defaultBindingPath}");

        TryEnsureOnGameAsset();
        ReinitializeRuntimeAndRepopulateUi();
        return slot.Action;
    }

    internal static bool TryGetAction(string modId, string actionId, out InputAction? action)
    {
        lock (LockObj)
        {
            foreach (ModKeybindSlot s in Slots)
            {
                if (string.Equals(s.Registration.ModId, modId, StringComparison.OrdinalIgnoreCase)
                 && string.Equals(s.Registration.ActionId, actionId, StringComparison.OrdinalIgnoreCase))
                {
                    action = s.Action;
                    return true;
                }
            }
        }
        action = null;
        return false;
    }

    internal static void NotifyRegistrationsChanged()
    {
        TryEnsureOnGameAsset();
        ReinitializeRuntimeAndRepopulateUi();
    }

    internal static void PrefixApplyControlSettings(SettingsPersistentSO __instance)
    {
        try
        {
            InputActionAsset? asset = Traverse.Create(__instance).Field<InputActionAsset>("_inputActions").Value;
            if (asset != null)
                EnsureActionsOnAsset(asset);
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogWarning($"ModKeybind ApplyControlSettings prefix: {ex.Message}");
        }
    }

    internal static void PrefixRuntimeInitialize(SettingsRebindRuntimeInfo __instance)
    {
        try
        {
            InputActionAsset? asset = FindGameInputAsset();
            if (asset != null)
                EnsureActionsOnAsset(asset);
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogWarning($"ModKeybind RuntimeInfo.Initialize prefix: {ex.Message}");
        }
    }

    internal static void PostfixRuntimeInitialize(SettingsRebindRuntimeInfo __instance)
    {
        try
        {
            InjectIntoRuntimeInfo(__instance);
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogError($"ModKeybind RuntimeInfo.Initialize postfix: {ex}");
        }
    }

    internal static void PostfixLocalizedName(SettingsRebindActionData __instance, ref string __result)
    {
        lock (LockObj)
        {
            foreach (ModKeybindSlot s in Slots)
            {
                if (ReferenceEquals(s.Data, __instance))
                {
                    string? locKey = s.Registration.DisplayNameLocalizationKey;
                    if (string.IsNullOrEmpty(locKey))
                        __result = s.Registration.DisplayName;
                    else
                        __result = ModL10n.Get(s.Registration.ModId, locKey!, s.Registration.DisplayName);
                    return;
                }
            }
        }
    }

    internal static void PostfixGroupLocalizedName(SettingsRebindGroup __instance, ref string __result)
    {
        lock (LockObj)
        {
            if (GroupDisplayTitle.TryGetValue(__instance, out string? title))
                __result = title;
        }
    }

    internal static void EnableModMapForGameplay()
    {
        try
        {
            InputActionAsset? asset = FindGameInputAsset();
            InputActionMap? map = asset?.FindActionMap(MapName);
            map?.Enable();
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogWarning($"ModKeybind EnableModMap: {ex.Message}");
        }
    }

    internal static void OnModsLoadedStageComplete()
    {
        TryEnsureOnGameAsset();
        ReinitializeRuntimeAndRepopulateUi();
    }

    private static void ReinitializeRuntimeAndRepopulateUi()
    {
        ReInitializeAllRuntimeInfos();
        RepopulateAllUi();
    }

    private static void ReInitializeAllRuntimeInfos()
    {
        SettingsRebindRuntimeInfo[] infos = FindAllScriptableObjects<SettingsRebindRuntimeInfo>();
        if (infos.Length == 0)
        {
            ModulusModLoaderPlugin.Log?.LogWarning(
                "ModKeybind: no SettingsRebindRuntimeInfo found in loaded assets; injection deferred.");
            return;
        }

        var seen = new HashSet<int>();
        foreach (SettingsRebindRuntimeInfo? info in infos)
        {
            if (info == null) continue;
            int id = info.GetInstanceID();
            if (!seen.Add(id)) continue;
            try { info.Initialize(); }
            catch (Exception ex)
            {
                ModulusModLoaderPlugin.Log?.LogWarning($"ModKeybind re-Initialize: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// <see cref="UnityEngine.Object.FindObjectsByType{T}"/> only finds scene Components and
    /// active GameObjects, not loaded asset <see cref="ScriptableObject"/>s. Use
    /// <see cref="Resources.FindObjectsOfTypeAll(Type)"/> to enumerate ScriptableObjects that the
    /// game has loaded into memory.
    /// </summary>
    private static T[] FindAllScriptableObjects<T>() where T : ScriptableObject
    {
        UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(T));
        if (all.Length == 0) return Array.Empty<T>();
        var result = new List<T>(all.Length);
        foreach (UnityEngine.Object o in all)
        {
            if (o is T so && (so.hideFlags & HideFlags.NotEditable) != HideFlags.NotEditable)
                result.Add(so);
        }
        return result.ToArray();
    }

    private static void RepopulateAllUi()
    {
        try
        {
            SettingsControlsPopulator[] pops = UnityEngine.Object.FindObjectsByType<SettingsControlsPopulator>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (pops.Length == 0) return;
            foreach (SettingsControlsPopulator pop in pops)
            {
                if (pop == null) continue;
                RepopulateOne(pop);
            }
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogWarning($"ModKeybind RepopulateAllUi: {ex.Message}");
        }
    }

    private static void RepopulateOne(SettingsControlsPopulator populator)
    {
        Traverse t = Traverse.Create(populator);
        SettingsControlsSubtitle? groupPrefab = t.Field<SettingsControlsSubtitle>("_rebindGroupPrefab").Value;
        SettingsRebindActionUI? rowPrefab    = t.Field<SettingsRebindActionUI>("_rebindActionPrefab").Value;
        GameObject?              spacerPrefab = t.Field<GameObject>("_spacerPrefab").Value;
        Transform?               resetButton  = t.Field<Transform>("_resetButton").Value;
        var uiList     = t.Field<List<SettingsRebindActionUI>>("_settingsRebindActionUIs").Value;
        var subtitles  = t.Field<List<(SettingsControlsSubtitle, SettingsRebindAction)>>("_subtitles").Value;
        if (rowPrefab == null) return;

        Transform parent = rowPrefab.transform.parent;
        var keep = new HashSet<Transform>();
        if (groupPrefab != null) keep.Add(groupPrefab.transform);
        if (rowPrefab   != null) keep.Add(rowPrefab.transform);
        if (spacerPrefab != null) keep.Add(spacerPrefab.transform);
        if (resetButton  != null) keep.Add(resetButton);

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform c = parent.GetChild(i);
            if (keep.Contains(c)) continue;
            UnityEngine.Object.Destroy(c.gameObject);
        }

        uiList?.Clear();
        subtitles?.Clear();

        try { populator.Populate(); }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogError($"ModKeybind Populate failed: {ex}");
        }
    }

    private static void TryEnsureOnGameAsset()
    {
        InputActionAsset? asset = FindGameInputAsset();
        if (asset != null)
            EnsureActionsOnAsset(asset);
    }

    private static InputActionAsset? FindGameInputAsset()
    {
        foreach (SettingsPersistentSO? so in FindAllScriptableObjects<SettingsPersistentSO>())
        {
            if (so == null) continue;
            InputActionAsset? asset = Traverse.Create(so).Field<InputActionAsset>("_inputActions").Value;
            if (asset != null)
                return asset;
        }
        return null;
    }

    private static void EnsureActionsOnAsset(InputActionAsset asset)
    {
        lock (LockObj)
        {
            if (Slots.Count == 0) return;

            // The Input System forbids adding maps/actions/bindings while ANY action in the asset
            // is enabled. Decide up front whether we actually need to mutate; if everything is
            // already present we can skip the disable/enable dance entirely.
            bool needsMap = asset.FindActionMap(MapName) == null;
            bool needsAction = false;
            if (!needsMap)
            {
                InputActionMap existing = asset.FindActionMap(MapName)!;
                foreach (ModKeybindSlot slot in Slots)
                {
                    if (existing.FindAction(slot.ImpulseName) == null)
                    {
                        needsAction = true;
                        break;
                    }
                }
            }

            InputActionMap? map = asset.FindActionMap(MapName);

            if (needsMap || needsAction)
            {
                List<InputActionMap> previouslyEnabled = new();
                foreach (InputActionMap m in asset.actionMaps)
                {
                    if (m.enabled)
                    {
                        previouslyEnabled.Add(m);
                        m.Disable();
                    }
                }

                try
                {
                    if (map == null)
                    {
                        map = new InputActionMap(MapName);
                        asset.AddActionMap(map);
                        ModulusModLoaderPlugin.Log?.LogInfo($"ModKeybind: created action map '{MapName}'.");
                    }

                    foreach (ModKeybindSlot slot in Slots)
                    {
                        InputAction? action = map.FindAction(slot.ImpulseName);
                        if (action == null)
                        {
                            action = map.AddAction(slot.ImpulseName, InputActionType.Button);
                            action.AddBinding(new InputBinding
                            {
                                path = slot.Registration.DefaultBindingPath,
                                id   = slot.Registration.BindingGuid,
                            });
                            ModulusModLoaderPlugin.Log?.LogInfo(
                                $"ModKeybind: added action {slot.ImpulseName} (binding {slot.Registration.BindingGuid:D}) on map '{MapName}'.");
                        }
                    }
                }
                finally
                {
                    foreach (InputActionMap m in previouslyEnabled)
                        m.Enable();
                }
            }

            if (map == null) return;
            foreach (ModKeybindSlot slot in Slots)
            {
                InputAction? action = map.FindAction(slot.ImpulseName);
                if (action != null)
                    slot.BindRuntime(action);
            }
        }
    }

    private static SettingsRebindGroup EnsureCategoryGroup(string categoryDisplay)
    {
        lock (LockObj)
        {
            if (CategoryGroups.TryGetValue(categoryDisplay, out SettingsRebindGroup? existing))
                return existing;

            SettingsRebindGroup g = ScriptableObject.CreateInstance<SettingsRebindGroup>();
            g.name = "ModulusModLoader_Grp_" + ShortHash(categoryDisplay);
            FieldInfo? loc = typeof(SettingsRebindGroup).GetField(
                "_groupLocName", BindingFlags.Instance | BindingFlags.NonPublic);
            loc?.SetValue(g, categoryDisplay);
            CategoryGroups[categoryDisplay] = g;
            GroupDisplayTitle[g] = categoryDisplay;
            return g;
        }
    }

    private static string ShortHash(string s)
    {
        using var sha = SHA256.Create();
        byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        return BitConverter.ToUInt32(h, 0).ToString("x8");
    }

    private static void InjectIntoRuntimeInfo(SettingsRebindRuntimeInfo runtimeInfo)
    {
        lock (LockObj)
        {
            if (Slots.Count == 0) return;

            ModulusModLoaderPlugin.Log?.LogInfo(
                $"ModKeybind: injecting {Slots.Count} action(s) into SettingsRebindRuntimeInfo (instance {runtimeInfo.GetInstanceID()}).");

            InputActionAsset? gameAsset = FindGameInputAsset();
            if (gameAsset != null)
                EnsureActionsOnAsset(gameAsset);

            Traverse t = Traverse.Create(runtimeInfo);
            SettingsRebindDatabase? database = t.Field<SettingsRebindDatabase>("_database").Value;
            if (database == null)
            {
                ModulusModLoaderPlugin.Log?.LogWarning("ModKeybind: runtime info has no _database; skipping inject.");
                return;
            }

            var rebindActions    = t.Field<List<SettingsRebindAction>>("_rebindActions").Value!;
            var datasByRef       = t.Field<Dictionary<InputActionReference, List<SettingsRebindActionData>>>("_rebindDatasByInputActions").Value!;
            var actionsByRef     = t.Field<Dictionary<InputActionReference, List<SettingsRebindAction>>>("_rebindActionsByInputActions").Value!;
            var actionsByGroup   = t.Field<Dictionary<SettingsRebindGroup, List<SettingsRebindAction>>>("_rebindActionsByGroup").Value!;
            var conflictByGroup  = t.Field<Dictionary<SettingsRebindGroup, List<SettingsRebindGroup>>>("_conflictingGroupByGroup").Value!;

            IOrderedEnumerable<ModKeybindSlot> ordered = Slots
                .OrderBy(s => s.Registration.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Registration.ModId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Registration.ActionId, StringComparer.OrdinalIgnoreCase);

            foreach (ModKeybindSlot slot in ordered)
            {
                if (slot.ActionReference == null || slot.Action == null)
                {
                    ModulusModLoaderPlugin.Log?.LogDebug(
                        $"ModKeybind: skipping inject for {slot.Registration.ModId}/{slot.Registration.ActionId} - asset not ready.");
                    continue;
                }

                int idx = slot.Action.bindings.IndexOf((b) => b.id == slot.Registration.BindingGuid);
                if (idx < 0)
                {
                    ModulusModLoaderPlugin.Log?.LogWarning(
                        $"ModKeybind: binding GUID missing on action {slot.Registration.ModId}/{slot.Registration.ActionId}; re-adding.");
                    slot.Action.AddBinding(new InputBinding
                    {
                        path = slot.Registration.DefaultBindingPath,
                        id   = slot.Registration.BindingGuid,
                    });
                }

                InputActionReference actionRef = slot.ActionReference;
                SettingsRebindGroup group = EnsureCategoryGroup(slot.Registration.Category);

                if (!datasByRef.TryGetValue(actionRef, out List<SettingsRebindActionData>? dataList))
                {
                    dataList = new List<SettingsRebindActionData>();
                    datasByRef.Add(actionRef, dataList);
                }
                if (!dataList.Contains(slot.Data))
                    dataList.Add(slot.Data);

                if (!actionsByRef.TryGetValue(actionRef, out List<SettingsRebindAction>? actList))
                {
                    actList = new List<SettingsRebindAction>();
                    actionsByRef.Add(actionRef, actList);
                }

                SettingsRebindAction sra;
                try
                {
                    sra = new SettingsRebindAction(slot.Data, group, database, false);
                }
                catch (Exception ex)
                {
                    ModulusModLoaderPlugin.Log?.LogError(
                        $"ModKeybind: failed to build SettingsRebindAction for {slot.Registration.ModId}/{slot.Registration.ActionId}: {ex.Message}");
                    continue;
                }

                rebindActions.Add(sra);
                actList.Add(sra);

                if (!actionsByGroup.TryGetValue(group, out List<SettingsRebindAction>? groupList))
                {
                    groupList = new List<SettingsRebindAction>();
                    actionsByGroup.Add(group, groupList);
                }
                groupList.Add(sra);

                if (!conflictByGroup.ContainsKey(group))
                    conflictByGroup[group] = new List<SettingsRebindGroup> { group };

                ModulusModLoaderPlugin.Log?.LogInfo(
                    $"ModKeybind: injected '{slot.Registration.DisplayName}' under category '{slot.Registration.Category}' (group {group.GetInstanceID()}). _rebindActions size now {rebindActions.Count}.");
            }
        }
    }

    private static Guid StableBindingGuid(string modId, string actionId)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("Modulus.ModKeybind.v1:" + modId + ":" + actionId));
        var g = new byte[16];
        Array.Copy(hash, g, 16);
        return new Guid(g);
    }

    private sealed class ModKeybindSlot
    {
        internal ModKeybindRegistration Registration;
        internal readonly SettingsRebindActionData Data;
        internal InputActionReference? ActionReference;
        internal readonly string ImpulseName;
        internal InputAction? Action;

        internal ModKeybindSlot(ModKeybindRegistration registration)
        {
            Registration = registration;
            ImpulseName  = BuildImpulseName(registration.ModId, registration.ActionId);
            Data = new SettingsRebindActionData
            {
                Action                 = null!,
                HiddenDuplicateActions = new List<InputActionReference>(),
                IsHoldAction           = false,
                AddUISpaceAbove        = false,
                IsHidden               = false,
                ModifierBindingId      = string.Empty,
                BindingId              = registration.BindingGuid.ToString("D"),
                AltModifierBindingId   = string.Empty,
                AltBindingId           = string.Empty,
            };
            SetLocNameField(Data, "ModLoader.Placeholder");
        }

        internal void BindRuntime(InputAction action)
        {
            if (action == null) return;
            Action = action;
            ActionReference = InputActionReference.Create(action);
            if (ActionReference == null)
            {
                ModulusModLoaderPlugin.Log?.LogError(
                    "ModKeybind: InputActionReference.Create returned null (unexpected).");
                return;
            }
            Data.Action = ActionReference;
        }

        private static string BuildImpulseName(string modId, string actionId)
        {
            using var sha = SHA256.Create();
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(modId + "\0" + actionId));
            ulong v = BitConverter.ToUInt64(h, 0) ^ BitConverter.ToUInt64(h, 8);
            return "M_" + (v & 0xFFFFFFFFFFFFUL).ToString("x12");
        }

        private static void SetLocNameField(SettingsRebindActionData data, string value)
        {
            FieldInfo? f = typeof(SettingsRebindActionData).GetField(
                "_locName", BindingFlags.Instance | BindingFlags.NonPublic);
            f?.SetValue(data, value);
        }
    }

    /// <summary>Reference equality for ScriptableObject / runtime groups.</summary>
    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceEqualityComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
