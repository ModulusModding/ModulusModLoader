using Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModulusModLoader;

/// <summary>
/// Minimal bottom-left strip: "ModulusModLoader v0.x.x — N mod(s) loaded".
/// Uses the game's own font/colors. Auto-sizes to text so it never clips.
/// </summary>
internal sealed class ModMenuCornerHud : MonoBehaviour
{
    private const string RootName = "ModulusModLoader_MenuHud";
    private TextMeshProUGUI? _label;

    internal static void EnsureOn(StartScreen startScreen)
    {
        Canvas? rootCanvas = startScreen.GetComponentInParent<Canvas>()?.rootCanvas
                          ?? startScreen.GetComponentInChildren<Canvas>(true)?.rootCanvas;
        Transform parent = rootCanvas != null ? rootCanvas.transform : startScreen.transform;

        if (parent.Find(RootName) != null)
            return;

        GameUiStyle style = GameUiStyle.For(startScreen);

        // ── Root: full-canvas anchor, ignored by any layout ──────────────────
        var root   = new GameObject(RootName);
        root.transform.SetParent(parent, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        root.AddComponent<LayoutElement>().ignoreLayout = true;

        // ── Pill: bottom-left, auto-sized to its text ────────────────────────
        var pill   = new GameObject("Pill");
        pill.transform.SetParent(root.transform, false);

        // Image first so we get a RectTransform for sure
        var pillImg   = pill.AddComponent<Image>();
        pillImg.color = new Color(style.ColorPanelBg.r,
                                  style.ColorPanelBg.g,
                                  style.ColorPanelBg.b, 0.82f);
        pillImg.raycastTarget = false;
        if (style.PanelSprite != null)
        {
            pillImg.sprite = style.PanelSprite;
            pillImg.type   = Image.Type.Sliced;
        }

        var pillRt  = pill.GetComponent<RectTransform>();
        pillRt.anchorMin = new Vector2(0f, 0f);
        pillRt.anchorMax = new Vector2(0f, 0f);
        pillRt.pivot     = new Vector2(0f, 0f);

        // Auto-size pill to its text content
        var pillFitter = pill.AddComponent<ContentSizeFitter>();
        pillFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        pillFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var hLayout = pill.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding           = new RectOffset(12, 12, 6, 6);
        hLayout.childAlignment    = TextAnchor.MiddleLeft;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;

        // ── Label inside pill ─────────────────────────────────────────────────
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(pill.transform, false);

        var txt = labelGo.AddComponent<TextMeshProUGUI>();
        txt.font      = style.Font;
        txt.fontSize  = style.FontSizeSmall;
        txt.color     = style.ColorTextMuted;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.raycastTarget      = false;
        txt.textWrappingMode = TextWrappingModes.NoWrap;

        // ── Anchor position: 14px from bottom-left safe area ─────────────────
        float scaleFactor = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        if (scaleFactor <= 0f) scaleFactor = 1f;
        float x = Screen.safeArea.xMin / scaleFactor + 14f;
        float y = Screen.safeArea.yMin / scaleFactor + 14f;
        pillRt.anchoredPosition = new Vector2(x, y);

        root.AddComponent<ModMenuCornerHud>().Init(txt);
    }

    private void Init(TextMeshProUGUI label)
    {
        _label = label;
        RefreshText();
    }

    private void Start() => RefreshText();

    private void RefreshText()
    {
        if (_label == null) return;

        if (!ModLoadReport.HasCompleted)
        {
            _label.text = $"{LoaderPluginInfo.Name} v{LoaderPluginInfo.Version}";
            return;
        }

        int n = ModLoadReport.PluginsLoaded;
        _label.text = n == 0
            ? $"{LoaderPluginInfo.Name} v{LoaderPluginInfo.Version}"
            : $"{LoaderPluginInfo.Name} v{LoaderPluginInfo.Version}  ·  {n} mod{(n == 1 ? "" : "s")} loaded";
    }
}
