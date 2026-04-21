using System.Reflection;
using Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModulusModLoader;

/// <summary>
/// Captures font/color/sprite values from the game's already-active main-menu objects.
/// Only reads from <see cref="StartScreen"/> (guaranteed active); does NOT look for
/// SettingsDisplay/SettingsMenu, which are lazy-loaded and absent at startup.
/// </summary>
internal sealed class GameUiStyle
{
    // ── Text ─────────────────────────────────────────────────────────────────
    internal readonly TMP_FontAsset Font;
    internal readonly float FontSizeTitle;
    internal readonly float FontSizeBody;
    internal readonly float FontSizeSmall;
    internal readonly Color ColorText;          // primary / label text
    internal readonly Color ColorTextMuted;     // secondary / id text

    // ── Panel / background ───────────────────────────────────────────────────
    internal readonly Color ColorPanelBg;       // main panel backdrop
    internal readonly Color ColorRowBg;         // mod row alternate shading
    internal readonly Color ColorDim;           // full-screen dim overlay

    // ── Button ───────────────────────────────────────────────────────────────
    internal readonly Sprite? ButtonSprite;
    internal readonly ColorBlock ButtonColors;

    // ── Toggle ───────────────────────────────────────────────────────────────
    internal readonly Sprite? ToggleBgSprite;
    internal readonly Sprite? ToggleCheckSprite;
    internal readonly ColorBlock ToggleColors;

    // ── Shared background Image from the menu sidebar ─────────────────────────
    internal readonly Sprite? PanelSprite;

    // ─────────────────────────────────────────────────────────────────────────

    private static GameUiStyle? _instance;

    internal static GameUiStyle For(StartScreen screen)
    {
        if (_instance != null)
            return _instance;
        _instance = Build(screen);
        return _instance;
    }

    /// Clear the cache (called when the scene unloads so a fresh capture happens next time).
    internal static void Invalidate() => _instance = null;

    // ── Defaults (used as fallback when a field can't be found) ────────────────
    private static readonly Color DefaultPanel   = new Color(0.086f, 0.094f, 0.122f, 0.97f);
    private static readonly Color DefaultRow     = new Color(0.12f,  0.13f,  0.16f,  0.8f);
    private static readonly Color DefaultDim     = new Color(0f,     0f,     0f,     0.6f);
    private static readonly Color DefaultText    = new Color(0.93f,  0.93f,  0.93f,  1f);
    private static readonly Color DefaultMuted   = new Color(0.56f,  0.60f,  0.66f,  1f);

    private static GameUiStyle Build(StartScreen screen)
    {
        // We can read these three buttons reliably
        Button? manualBtn    = GetField<Button>(screen, "_manualButton");
        Button? settingsBtn  = GetField<Button>(screen, "_settingsButton");
        Button? newGameBtn   = GetField<Button>(screen, "_newGameButton");
        GameObject? menuPanel = GetField<GameObject>(screen, "_mainMenuPanel");

        // ── Font + text color from the first TMP inside a main-menu button ─────
        TMP_FontAsset font  = TMP_Settings.defaultFontAsset;
        float sizeBody      = 18f;
        float sizeTitle     = 28f;
        float sizeSmall     = 14f;
        Color colorText     = DefaultText;
        Color colorMuted    = DefaultMuted;

        Button? refBtn = manualBtn ?? settingsBtn ?? newGameBtn;
        if (refBtn != null)
        {
            var tmpChild = refBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpChild != null)
            {
                if (tmpChild.font != null)
                    font = tmpChild.font;
                sizeBody  = tmpChild.fontSize > 0 ? tmpChild.fontSize : sizeBody;
                colorText = tmpChild.color.a > 0.01f ? tmpChild.color : colorText;
            }

            sizeTitle = Mathf.Round(sizeBody * 1.55f);
            sizeSmall = Mathf.Round(sizeBody * 0.78f);
            // muted = text but desaturated / dimmed
            Color.RGBToHSV(colorText, out float h, out _, out float v);
            colorMuted = Color.HSVToRGB(h, 0.18f, v * 0.68f);
            colorMuted.a = 1f;
        }

        // ── Button sprite + colors ─────────────────────────────────────────────
        Sprite?     buttonSprite = null;
        ColorBlock  buttonColors = ColorBlock.defaultColorBlock;

        if (refBtn != null)
        {
            buttonColors = refBtn.colors;
            if (refBtn.targetGraphic is Image bi && bi.sprite != null)
                buttonSprite = bi.sprite;
        }

        // ── Panel background color from the menu sidebar ───────────────────────
        Color  panelBg     = DefaultPanel;
        Sprite? panelSprite = null;

        if (menuPanel != null)
        {
            var panelImg = menuPanel.GetComponent<Image>();
            if (panelImg == null)
                panelImg = menuPanel.GetComponentInChildren<Image>(true);
            if (panelImg != null)
            {
                panelBg     = panelImg.color;
                panelSprite = panelImg.sprite;
            }
        }
        else if (refBtn != null)
        {
            // Walk up until we find an Image that isn't the button itself
            Transform? cur = refBtn.transform.parent;
            while (cur != null)
            {
                if (cur.TryGetComponent<Image>(out var img))
                {
                    panelBg     = img.color;
                    panelSprite = img.sprite;
                    break;
                }
                cur = cur.parent;
            }
        }

        // Row is panel darkened slightly
        Color rowBg = new Color(
            panelBg.r * 1.18f,
            panelBg.g * 1.18f,
            panelBg.b * 1.18f,
            0.85f);

        // ── Toggle (search entire scene — might already be active in HUD etc.) ──
        Sprite?    toggleBg    = null;
        Sprite?    toggleCheck = null;
        ColorBlock toggleClrs  = ColorBlock.defaultColorBlock;

        var allToggles = UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allToggles)
        {
            if (t == null) continue;
            toggleClrs = t.colors;
            if (t.targetGraphic is Image ti && ti.sprite != null)
                toggleBg = ti.sprite;
            if (t.graphic is Image gi && gi.sprite != null)
                toggleCheck = gi.sprite;
            if (toggleBg != null && toggleCheck != null)
                break;
        }

        return new GameUiStyle(
            font, sizeTitle, sizeBody, sizeSmall,
            colorText, colorMuted,
            panelBg, rowBg, DefaultDim,
            buttonSprite, buttonColors,
            toggleBg, toggleCheck, toggleClrs,
            panelSprite);
    }

    private GameUiStyle(
        TMP_FontAsset font, float title, float body, float small,
        Color text, Color muted,
        Color panel, Color row, Color dim,
        Sprite? btnSprite, ColorBlock btnColors,
        Sprite? togBg, Sprite? togCheck, ColorBlock togColors,
        Sprite? panelSprite)
    {
        Font              = font;
        FontSizeTitle     = title;
        FontSizeBody      = body;
        FontSizeSmall     = small;
        ColorText         = text;
        ColorTextMuted    = muted;
        ColorPanelBg      = panel;
        ColorRowBg        = row;
        ColorDim          = dim;
        ButtonSprite      = btnSprite;
        ButtonColors      = btnColors;
        ToggleBgSprite    = togBg;
        ToggleCheckSprite = togCheck;
        ToggleColors      = togColors;
        PanelSprite       = panelSprite;
    }

    private static T? GetField<T>(object obj, string name) where T : class
    {
        for (var t = obj.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f?.GetValue(obj) is T val)
                return val;
        }
        return null;
    }
}
