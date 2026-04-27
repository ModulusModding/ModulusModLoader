using Data.SaveData.PersistentSOs;
using Data.UI.Controls;
using HarmonyLib;

namespace ModulusModLoader.Patches;

[HarmonyPatch(typeof(SettingsPersistentSO), "ApplyControlSettings", new[] { typeof(ControlsSettingsSaveData) })]
internal static class SettingsPersistentApplyControlsPatch
{
    [HarmonyPrefix]
    private static void Prefix(SettingsPersistentSO __instance) =>
        ModKeybindIntegration.PrefixApplyControlSettings(__instance);
}

[HarmonyPatch(typeof(SettingsRebindRuntimeInfo), nameof(SettingsRebindRuntimeInfo.Initialize))]
internal static class SettingsRebindRuntimeInfoInitializePatch
{
    [HarmonyPrefix]
    private static void Prefix(SettingsRebindRuntimeInfo __instance) =>
        ModKeybindIntegration.PrefixRuntimeInitialize(__instance);

    [HarmonyPostfix]
    private static void Postfix(SettingsRebindRuntimeInfo __instance) =>
        ModKeybindIntegration.PostfixRuntimeInitialize(__instance);
}

[HarmonyPatch(typeof(SettingsRebindActionData), nameof(SettingsRebindActionData.GetLocalizedName))]
internal static class SettingsRebindActionDataLocaPatch
{
    [HarmonyPostfix]
    private static void Postfix(SettingsRebindActionData __instance, ref string __result) =>
        ModKeybindIntegration.PostfixLocalizedName(__instance, ref __result);
}

[HarmonyPatch(typeof(SettingsRebindGroup), nameof(SettingsRebindGroup.GetLocalizedName))]
internal static class SettingsRebindGroupLocaPatch
{
    [HarmonyPostfix]
    private static void Postfix(SettingsRebindGroup __instance, ref string __result) =>
        ModKeybindIntegration.PostfixGroupLocalizedName(__instance, ref __result);
}
