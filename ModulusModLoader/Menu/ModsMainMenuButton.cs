using System.Reflection;
using Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModulusModLoader;

/// <summary>
/// Adds a &quot;Mods&quot; button next to the main menu controls (cloned from Manual).
/// The Manual prefab includes <see cref="LocalizedTMPText"/> with the manual string id; that component must be
/// removed from the clone or every <c>LocalizationUtility.OnLanguageUpdate</c> overwrites the label with Handbuch again.
/// </summary>
internal static class ModsMainMenuButton
{
    internal const string ButtonObjectName = "ModulusModLoader_ModsButton";

    private static readonly FieldInfo ManualButtonField =
        typeof(StartScreen).GetField("_manualButton", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void EnsureOn(StartScreen startScreen)
    {
        foreach (Transform t in startScreen.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != ButtonObjectName)
                continue;
            StripVanillaLocalizationAndSetModsLabel(t.gameObject);
            return;
        }

        if (ManualButtonField.GetValue(startScreen) is not Button manual)
        {
            ModulusModLoaderPlugin.Log?.LogWarning("Mods button: _manualButton not found on StartScreen.");
            return;
        }

        GameObject clone = Object.Instantiate(manual.gameObject, manual.transform.parent);
        clone.name = ButtonObjectName;
        clone.transform.SetSiblingIndex(manual.transform.GetSiblingIndex() + 1);

        var btn = clone.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(new UnityAction(() => ModsMenuPanel.Toggle(startScreen)));

        StripVanillaLocalizationAndSetModsLabel(clone);
    }

    private static void StripVanillaLocalizationAndSetModsLabel(GameObject root)
    {
        foreach (var loc in root.GetComponentsInChildren<LocalizedTMPText>(true))
            Object.Destroy(loc);

        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            tmp.text = "Mods";
            break;
        }
    }
}
