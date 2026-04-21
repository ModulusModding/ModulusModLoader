using HarmonyLib;
using Presentation.UI;

namespace ModulusModLoader.Patches;

/// <summary>
/// Top-left HUD with loader status (StarshipEvo-style) when the main menu is shown.
/// </summary>
[HarmonyPatch(typeof(StartScreen), nameof(StartScreen.ShowMainMenu))]
internal static class StartScreenShowMainMenuPatch
{
    [HarmonyPostfix]
    private static void Postfix(StartScreen __instance)
    {
        ModMenuCornerHud.EnsureOn(__instance);
        ModsMainMenuButton.EnsureOn(__instance);
    }
}
