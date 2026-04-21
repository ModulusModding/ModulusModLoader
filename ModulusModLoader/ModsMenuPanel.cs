using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModulusModLoader;

/// <summary>
/// Full-screen mods dialog: left column = drag-reorderable list with enable toggles;
/// right column = info pane showing About.xml content (BBCode rendered).
/// Clones visual style from the active StartScreen main-menu buttons.
/// </summary>
internal static class ModsMenuPanel
{
    private static GameObject? _root;

    internal static void Toggle(StartScreen host)
    {
        if (_root != null) { Close(); return; }
        ModRegistry.EnsureLoaded(ModulusModLoaderPlugin.Log);
        Build(host);
    }

    internal static void Close()
    {
        if (_root == null) return;
        Object.Destroy(_root);
        _root = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static void Build(StartScreen host)
    {
        GameUiStyle style = GameUiStyle.For(host);

        Canvas? rootCanvas = host.GetComponentInParent<Canvas>()?.rootCanvas
                          ?? host.GetComponentInChildren<Canvas>(true)?.rootCanvas;
        Transform parent   = rootCanvas != null ? rootCanvas.transform : host.transform;

        // ── Overlay root ──────────────────────────────────────────────────────
        _root = new GameObject("ModulusModLoader_ModsOverlay");
        _root.transform.SetParent(parent, false);

        // RectTransform BEFORE LayoutElement (which [RequireComponent]s it)
        var overlayRt     = _root.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        _root.AddComponent<LayoutElement>().ignoreLayout = true;
        _root.transform.SetAsLastSibling();

        var canvas = _root.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 5000;
        _root.AddComponent<GraphicRaycaster>();

        // ── Dim ───────────────────────────────────────────────────────────────
        var dim   = FullStretch("Dim", _root.transform);
        var dimI  = dim.AddComponent<Image>();
        dimI.color = style.ColorDim;
        var dimB  = dim.AddComponent<Button>();
        dimB.targetGraphic = dimI;
        dimB.transition    = Selectable.Transition.None;
        dimB.onClick.AddListener(Close);

        // ── Panel ─────────────────────────────────────────────────────────────
        var panel   = new GameObject("Panel");
        panel.transform.SetParent(_root.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.08f, 0.06f);
        panelRt.anchorMax = new Vector2(0.92f, 0.94f);
        panelRt.pivot     = new Vector2(0.5f,  0.5f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        var panelImg  = panel.AddComponent<Image>();
        panelImg.color = style.ColorPanelBg;
        if (style.PanelSprite != null) { panelImg.sprite = style.PanelSprite; panelImg.type = Image.Type.Sliced; }

        var panelVlg  = panel.AddComponent<VerticalLayoutGroup>();
        panelVlg.padding            = new RectOffset(20, 20, 16, 16);
        panelVlg.spacing            = 10f;
        panelVlg.childAlignment     = TextAnchor.UpperCenter;
        panelVlg.childControlHeight = true;
        panelVlg.childControlWidth  = true;
        panelVlg.childForceExpandHeight = false;
        panelVlg.childForceExpandWidth  = true;

        // ── Header row: title + close button ──────────────────────────────────
        var header    = MakeHBox(panel.transform, 0f, 48f);
        var titleGo   = AddTmp(header.transform, "MODS", style.Font, style.FontSizeTitle,
                               style.ColorText, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        titleGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Custom close button — never clone the game button (it carries icon sprites)
        AddCloseButton(header.transform, style);

        // ── Separator ─────────────────────────────────────────────────────────
        AddSeparator(panel.transform, style);

        // ── Body: left list | divider | right info ────────────────────────────
        var body    = new GameObject("Body");
        body.transform.SetParent(panel.transform, false);
        // Image with transparent color for RectTransform, then LayoutElement
        var bodyImg = body.AddComponent<Image>(); bodyImg.color = Color.clear;
        body.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var bodyHlg = body.AddComponent<HorizontalLayoutGroup>();
        bodyHlg.spacing             = 0f;
        bodyHlg.childAlignment      = TextAnchor.UpperLeft;
        bodyHlg.childControlHeight  = true;
        bodyHlg.childControlWidth   = true;
        bodyHlg.childForceExpandHeight = true;
        bodyHlg.childForceExpandWidth  = false;

        // ─── Left: list ───────────────────────────────────────────────────────
        var leftPane    = new GameObject("ListPane");
        leftPane.transform.SetParent(body.transform, false);
        var leftImg     = leftPane.AddComponent<Image>(); leftImg.color = Color.clear;
        var leftLe      = leftPane.AddComponent<LayoutElement>();
        leftLe.flexibleWidth  = 1f;
        leftLe.minWidth       = 200f;
        var leftVlg     = leftPane.AddComponent<VerticalLayoutGroup>();
        leftVlg.spacing             = 8f;
        leftVlg.childAlignment      = TextAnchor.UpperLeft;
        leftVlg.childControlHeight  = true;
        leftVlg.childControlWidth   = true;
        leftVlg.childForceExpandHeight = false;
        leftVlg.childForceExpandWidth  = true;

        // ─── Divider ──────────────────────────────────────────────────────────
        var divGo   = new GameObject("Div");
        divGo.transform.SetParent(body.transform, false);
        var divImg  = divGo.AddComponent<Image>();
        divImg.color = new Color(style.ColorText.r, style.ColorText.g, style.ColorText.b, 0.1f);
        var divLe   = divGo.AddComponent<LayoutElement>();
        divLe.minWidth = 1f; divLe.preferredWidth = 1f; divLe.flexibleWidth = 0f;

        // ─── Right: info pane (takes ~42 % of body via flex ratio 1 : 0.72) ───
        var rightPane   = new GameObject("InfoPane");
        rightPane.transform.SetParent(body.transform, false);
        // Slight inner background so the pane reads as a separate section
        var rightImg    = rightPane.AddComponent<Image>();
        rightImg.color  = new Color(style.ColorPanelBg.r * 0.7f,
                                    style.ColorPanelBg.g * 0.7f,
                                    style.ColorPanelBg.b * 0.8f, 0.55f);
        var rightLe     = rightPane.AddComponent<LayoutElement>();
        rightLe.minWidth      = 220f;
        rightLe.flexibleWidth = 0.72f;   // left = 1f  →  right ≈ 42 %
        rightLe.flexibleHeight = 1f;
        var rightVlg    = rightPane.AddComponent<VerticalLayoutGroup>();
        rightVlg.spacing             = 8f;
        rightVlg.padding             = new RectOffset(18, 12, 12, 12);
        rightVlg.childAlignment      = TextAnchor.UpperLeft;
        rightVlg.childControlHeight  = true;
        rightVlg.childControlWidth   = true;
        rightVlg.childForceExpandHeight = false;
        rightVlg.childForceExpandWidth  = true;

        // Placeholder when nothing is selected
        var placeholder = AddTmp(rightPane.transform, "← Select a mod to view details",
            style.Font, style.FontSizeSmall, style.ColorTextMuted,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        placeholder.AddComponent<LayoutElement>().flexibleHeight = 1f;
        placeholder.GetComponent<TextMeshProUGUI>().textWrappingMode = TextWrappingModes.Normal;

        // Info content (hidden until a mod is selected)
        var infoContent = new GameObject("InfoContent");
        infoContent.transform.SetParent(rightPane.transform, false);
        var infoImg     = infoContent.AddComponent<Image>(); infoImg.color = Color.clear;
        var infoLe      = infoContent.AddComponent<LayoutElement>(); infoLe.flexibleHeight = 1f;
        var infoVlg     = infoContent.AddComponent<VerticalLayoutGroup>();
        infoVlg.spacing            = 6f;
        infoVlg.childAlignment     = TextAnchor.UpperLeft;
        infoVlg.childControlHeight = true;
        infoVlg.childControlWidth  = true;
        infoVlg.childForceExpandHeight = false;
        infoVlg.childForceExpandWidth  = true;
        infoContent.SetActive(false);

        // Name (large)
        var infoName = AddTmp(infoContent.transform, "",
            style.Font, style.FontSizeTitle * 0.85f, style.ColorText,
            FontStyles.Bold, TextAlignmentOptions.TopLeft);
        infoName.GetComponent<TextMeshProUGUI>().textWrappingMode = TextWrappingModes.Normal;

        // Author / Version / ID (muted, monospace-ish via TMP)
        var infoDetail = AddTmp(infoContent.transform, "",
            style.Font, style.FontSizeSmall, style.ColorTextMuted,
            FontStyles.Normal, TextAlignmentOptions.TopLeft);
        infoDetail.GetComponent<TextMeshProUGUI>().textWrappingMode = TextWrappingModes.Normal;

        AddSeparator(infoContent.transform, style);

        // Description in its own scroll rect
        var descScroll = BuildDescScrollRect(infoContent.transform, style);
        var infoDesc   = descScroll.GetComponentInChildren<TextMeshProUGUI>();

        // ─── Scroll rect for the mod list ────────────────────────────────────
        var listScroll = BuildListScrollRect(leftPane.transform);

        // ── Footer: hint text ─────────────────────────────────────────────────
        var footerTmp = AddTmp(panel.transform, "Changes take effect on next launch.  |  Drag ≡ to reorder",
            style.Font, style.FontSizeSmall, style.ColorTextMuted,
            FontStyles.Normal, TextAlignmentOptions.Center);
        footerTmp.AddComponent<LayoutElement>().minHeight = style.FontSizeSmall * 2f;
        footerTmp.GetComponent<TextMeshProUGUI>().textWrappingMode = TextWrappingModes.NoWrap;

        // ── Discover mods in load order ───────────────────────────────────────
        string modsRoot   = ModPaths.GetUserModsRoot(ModulusModLoaderPlugin.Log);
        var    allDescs   = ModFolderDiscovery.Discover(modsRoot, ModulusModLoaderPlugin.Log!);
        ModRegistry.MergeFromDiscovery(allDescs.Select(d => d.EffectiveModId));

        List<ModFolderDescriptor> ordered = ApplyLoadOrder(allDescs);

        // ── Drop indicator line ───────────────────────────────────────────────
        var indicatorGo  = new GameObject("DropIndicator");
        indicatorGo.transform.SetParent(listScroll.content, false);
        var indImg       = indicatorGo.AddComponent<Image>();
        indImg.color     = new Color(0.3f, 0.85f, 0.6f, 0.9f);
        var indRt        = indicatorGo.GetComponent<RectTransform>();
        indRt.sizeDelta  = new Vector2(0f, 2f);
        indRt.anchorMin  = new Vector2(0f, 1f);
        indRt.anchorMax  = new Vector2(1f, 1f);
        indRt.pivot      = new Vector2(0.5f, 1f);
        indicatorGo.SetActive(false);

        // ── ModListController wires everything together ────────────────────────
        var ctrl = _root.AddComponent<ModListController>();
        ctrl.Mods             = ordered;
        ctrl.Style            = style;
        ctrl.ContentRt        = listScroll.content;
        ctrl.ScrollRect       = listScroll;
        ctrl.DropIndicatorRt  = indRt;
        ctrl.OverlayCanvas    = canvas;
        ctrl.InfoNameTmp      = infoName.GetComponent<TextMeshProUGUI>();
        ctrl.InfoDetailTmp    = infoDetail.GetComponent<TextMeshProUGUI>();
        ctrl.InfoDescTmp      = infoDesc;
        ctrl.InfoPlaceholderGo = placeholder;
        ctrl.InfoContentGo    = infoContent;

        ctrl.Init();

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
        Canvas.ForceUpdateCanvases();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<ModFolderDescriptor> ApplyLoadOrder(
        List<ModFolderDescriptor> discovered)
    {
        List<string>? saved = ModRegistry.GetSavedLoadOrder();
        if (saved == null || saved.Count == 0)
            return ModFolderOrdering.SortByAbout(discovered, ModulusModLoaderPlugin.Log!);

        // Place mods in saved order; append any new ones at the end
        var byId  = discovered.ToDictionary(d => d.EffectiveModId,
                                            StringComparer.OrdinalIgnoreCase);
        var result = new List<ModFolderDescriptor>(discovered.Count);
        foreach (var id in saved)
        {
            if (byId.TryGetValue(id, out var d))
            { result.Add(d); byId.Remove(id); }
        }
        foreach (var d in byId.Values)
            result.Add(d);
        return result;
    }

    private static ScrollRect BuildListScrollRect(Transform parent)
    {
        var scrollGo  = new GameObject("ListScroll");
        scrollGo.transform.SetParent(parent, false);
        var scrollImg = scrollGo.AddComponent<Image>(); scrollImg.color = Color.clear;
        scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var scroll            = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal     = false;
        scroll.vertical       = true;
        scroll.movementType   = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var vp   = FullStretch("Viewport", scrollGo.transform);
        vp.AddComponent<RectMask2D>();

        var contentGo  = new GameObject("Content");
        contentGo.transform.SetParent(vp.transform, false);
        var contentRt  = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin       = new Vector2(0f, 1f);
        contentRt.anchorMax       = new Vector2(1f, 1f);
        contentRt.pivot           = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta       = Vector2.zero;

        var contentVlg = contentGo.AddComponent<VerticalLayoutGroup>();
        contentVlg.spacing            = 4f;
        contentVlg.padding            = new RectOffset(0, 0, 2, 2);
        contentVlg.childAlignment     = TextAnchor.UpperLeft;
        contentVlg.childControlHeight = true;
        contentVlg.childControlWidth  = true;
        contentVlg.childForceExpandWidth  = true;
        contentVlg.childForceExpandHeight = false;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vp.GetComponent<RectTransform>();
        scroll.content  = contentRt;

        return scroll;
    }

    private static ScrollRect BuildDescScrollRect(Transform parent, GameUiStyle style)
    {
        var scrollGo  = new GameObject("DescScroll");
        scrollGo.transform.SetParent(parent, false);
        scrollGo.AddComponent<Image>().color = new Color(
            style.ColorPanelBg.r * 0.7f, style.ColorPanelBg.g * 0.7f,
            style.ColorPanelBg.b * 0.7f, 0.6f);
        scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var scroll            = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal     = false;
        scroll.vertical       = true;
        scroll.movementType   = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var vp = FullStretch("Viewport", scrollGo.transform);
        vp.AddComponent<RectMask2D>();

        var contentGo  = new GameObject("Content");
        contentGo.transform.SetParent(vp.transform, false);
        var contentRt  = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin       = new Vector2(0f, 1f);
        contentRt.anchorMax       = new Vector2(1f, 1f);
        contentRt.pivot           = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta       = Vector2.zero;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var descTmp = contentGo.AddComponent<TextMeshProUGUI>();
        descTmp.font             = style.Font;
        descTmp.fontSize         = style.FontSizeSmall;
        descTmp.color            = style.ColorText;
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        descTmp.alignment        = TextAlignmentOptions.TopLeft;

        // Padding via RectTransform inset
        contentRt.offsetMin = new Vector2(8f,  8f);
        contentRt.offsetMax = new Vector2(-8f, 0f);

        scroll.viewport = vp.GetComponent<RectTransform>();
        scroll.content  = contentRt;

        return scroll;
    }

    private static void AddSeparator(Transform parent, GameUiStyle style)
    {
        var go = new GameObject("Sep");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(style.ColorText.r, style.ColorText.g, style.ColorText.b, 0.1f);
        go.AddComponent<LayoutElement>().minHeight = 1f;
    }

    private static GameObject AddTmp(Transform parent, string text,
        TMP_FontAsset font, float size, Color color, FontStyles fstyle,
        TextAlignmentOptions align)
    {
        var go  = new GameObject("Txt");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font      = font;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.fontStyle = fstyle;
        tmp.text      = text;
        tmp.alignment = align;
        return go;
    }

    /// Dedicated close button: always pure-text, never uses the game's icon sprite.
    private static void AddCloseButton(Transform parent, GameUiStyle style)
    {
        var go  = new GameObject("CloseBtn");
        go.transform.SetParent(parent, false);

        // Invisible background — only shows a faint circle on hover
        var img   = go.AddComponent<Image>();
        img.color = Color.clear;

        var le    = go.AddComponent<LayoutElement>();
        le.minWidth  = 44f; le.preferredWidth  = 44f;
        le.minHeight = 44f; le.preferredHeight = 44f;
        le.flexibleWidth = 0f;

        var btn   = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition    = Selectable.Transition.ColorTint;
        var bc    = ColorBlock.defaultColorBlock;
        bc.normalColor      = Color.clear;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        bc.pressedColor     = new Color(1f, 1f, 1f, 0.06f);
        bc.fadeDuration     = 0.08f;
        btn.colors          = bc;
        btn.onClick.AddListener(new UnityEngine.Events.UnityAction(Close));

        // Large X character as the only visual element
        var lbl   = new GameObject("X");
        lbl.transform.SetParent(go.transform, false);
        var lrt   = lbl.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var t     = lbl.AddComponent<TextMeshProUGUI>();
        t.font      = style.Font;
        t.fontSize  = style.FontSizeTitle;
        t.color     = style.ColorTextMuted;
        t.text      = "X";
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    private static void AddSimpleButton(Transform parent, string label,
        float minW, float minH, GameUiStyle style, Action onClick)
    {
        var go  = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(style.ColorText.r, style.ColorText.g, style.ColorText.b, 0.08f);
        var le  = go.AddComponent<LayoutElement>();
        le.minWidth = minW; le.preferredWidth = minW; le.flexibleWidth = 0f;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var bc  = ColorBlock.defaultColorBlock;
        bc.normalColor    = Color.white;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.22f);
        bc.pressedColor   = new Color(1f, 1f, 1f, 0.08f);
        btn.colors        = bc;
        btn.onClick.AddListener(new UnityEngine.Events.UnityAction(onClick));

        var lbl = new GameObject("Lbl");
        lbl.transform.SetParent(go.transform, false);
        var lblRt = lbl.AddComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
        var t = lbl.AddComponent<TextMeshProUGUI>();
        t.font = style.Font; t.fontSize = style.FontSizeBody;
        t.color = style.ColorText; t.text = label;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    /// Creates an HBox child with fixed height and transparent background.
    private static GameObject MakeHBox(Transform parent, float flexH, float minH)
    {
        var go = new GameObject("HBox");
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = Color.clear;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = flexH; le.minHeight = minH;
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing             = 8f;
        h.childAlignment      = TextAnchor.MiddleLeft;
        h.childControlHeight  = true;
        h.childControlWidth   = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth  = false;
        return go;
    }

    /// Creates a child that fills its parent via anchors. Returns the GameObject.
    private static GameObject FullStretch(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    private static T? GetField<T>(object obj, string name) where T : class
    {
        for (var t = obj.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f?.GetValue(obj) is T v) return v;
        }
        return null;
    }
}
