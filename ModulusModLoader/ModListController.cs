using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModulusModLoader;

// ─────────────────────────────────────────────────────────────────────────────
// ModListController  – manages the reorderable, selectable mod list and info pane
// ─────────────────────────────────────────────────────────────────────────────
internal sealed class ModListController : MonoBehaviour
{
    // Set before Init() by ModsMenuPanel.
    internal List<ModFolderDescriptor> Mods            = new();
    internal GameUiStyle               Style = default!;
    internal RectTransform             ContentRt        = null!;
    internal ScrollRect                ScrollRect       = null!;
    internal RectTransform             DropIndicatorRt  = null!;
    internal Canvas                    OverlayCanvas    = null!;

    // Info-pane widgets
    internal TextMeshProUGUI InfoNameTmp        = null!;
    internal TextMeshProUGUI InfoDetailTmp      = null!;
    internal TextMeshProUGUI InfoDescTmp        = null!;
    internal GameObject      InfoPlaceholderGo  = null!;
    internal GameObject      InfoContentGo      = null!;

    private int                    _selectedIndex = -1;
    private readonly List<ModRowItem> _rows       = new();

    // ── Public API ────────────────────────────────────────────────────────────

    internal void Init() => Rebuild();

    internal void SelectMod(int idx)
    {
        _selectedIndex = idx;
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].SetSelected(i == _selectedIndex, Style);
        RefreshInfoPane();
    }

    internal void ToggleMod(int idx)
    {
        if (idx < 0 || idx >= _rows.Count) return;
        _rows[idx].ToggleEnabled(Style);
    }

    internal void MoveItem(int fromIdx, int toIdx)
    {
        if (fromIdx < 0 || fromIdx >= Mods.Count) return;
        if (toIdx   < 0 || toIdx   >  Mods.Count) return;
        if (fromIdx == toIdx) return;

        var item    = Mods[fromIdx];
        Mods.RemoveAt(fromIdx);
        int insert  = Mathf.Clamp(toIdx > fromIdx ? toIdx - 1 : toIdx, 0, Mods.Count);
        Mods.Insert(insert, item);

        if (_selectedIndex == fromIdx)
            _selectedIndex = insert;

        ModRegistry.SetLoadOrder(Mods.Select(m => m.EffectiveModId).ToList(),
                                 ModulusModLoaderPlugin.Log);
        Rebuild();
    }

    internal int GetDropIndex(Vector2 screenPos, Camera? cam)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            ContentRt, screenPos, cam, out Vector2 local))
            return _rows.Count;

        for (int i = 0; i < _rows.Count; i++)
        {
            var rt  = _rows[i].GetComponent<RectTransform>();
            if (rt == null) continue;
            float top = rt.anchoredPosition.y;
            float bot = top - rt.rect.height;
            float mid = (top + bot) * 0.5f;
            if (local.y > mid) return i;
        }
        return _rows.Count;
    }

    internal void ShowDropIndicator(int targetIdx)
    {
        if (_rows.Count == 0) { DropIndicatorRt.gameObject.SetActive(false); return; }

        DropIndicatorRt.gameObject.SetActive(true);
        float y;
        if (targetIdx <= 0)
        {
            y = _rows[0].GetComponent<RectTransform>().anchoredPosition.y;
        }
        else if (targetIdx >= _rows.Count)
        {
            var last = _rows[_rows.Count - 1].GetComponent<RectTransform>();
            y = last.anchoredPosition.y - last.rect.height;
        }
        else
        {
            var above = _rows[targetIdx - 1].GetComponent<RectTransform>();
            var below = _rows[targetIdx].GetComponent<RectTransform>();
            y = (above.anchoredPosition.y - above.rect.height + below.anchoredPosition.y) * 0.5f;
        }
        DropIndicatorRt.anchoredPosition = new Vector2(0f, y);
    }

    internal void HideDropIndicator() => DropIndicatorRt.gameObject.SetActive(false);

    internal void OnDragBegin(int idx)
    {
        if (idx < 0 || idx >= _rows.Count) return;
        var c = _rows[idx].RowBg.color;
        _rows[idx].RowBg.color = new Color(c.r, c.g, c.b, 0.3f);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void Rebuild()
    {
        foreach (var r in _rows)
            if (r != null) Object.Destroy(r.gameObject);
        _rows.Clear();

        // Drop indicator lives at the END of content children so it draws on top
        DropIndicatorRt.gameObject.SetActive(false);

        for (int i = 0; i < Mods.Count; i++)
            _rows.Add(BuildRow(Mods[i], i));

        // Re-parent indicator to end
        DropIndicatorRt.SetAsLastSibling();

        for (int i = 0; i < _rows.Count; i++)
            _rows[i].SetSelected(i == _selectedIndex, Style);

        RefreshInfoPane();
        LayoutRebuilder.ForceRebuildLayoutImmediate(ContentRt);
        Canvas.ForceUpdateCanvases();
    }

    private ModRowItem BuildRow(ModFolderDescriptor d, int idx)
    {
        bool enabled = ModRegistry.IsEnabled(d.EffectiveModId);

        var row   = new GameObject("ModRow_" + idx);
        row.transform.SetParent(ContentRt, false);

        var rowBg = row.AddComponent<Image>();
        rowBg.color = RowBgColor(enabled, false);

        row.AddComponent<LayoutElement>().minHeight = 44f;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                 = 0f;
        hlg.childAlignment          = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth   = false;
        hlg.childForceExpandHeight  = true;
        hlg.childControlWidth       = true;
        hlg.childControlHeight      = true;
        hlg.padding                 = new RectOffset(0, 0, 0, 0);

        // ── Drag handle ───────────────────────────────────────────────────────
        //    Three stacked short lines — standard drag grip pattern.
        //    Uses the game's own font + a visible size so it actually renders.
        var handleGo = new GameObject("Grip");
        handleGo.transform.SetParent(row.transform, false);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color         = Color.clear;
        handleImg.raycastTarget = true;
        AddFixedLayout(handleGo, 30f, 44f);

        var gripTmp = MakeChildTmp(handleGo, Style);
        gripTmp.text      = "=";
        gripTmp.fontSize  = Style.FontSizeBody;
        gripTmp.color     = new Color(Style.ColorTextMuted.r, Style.ColorTextMuted.g,
                                      Style.ColorTextMuted.b, 0.45f);
        gripTmp.alignment = TextAlignmentOptions.Center;
        var drag = handleGo.AddComponent<ModRowDragHandler>();

        // ── Spacer ────────────────────────────────────────────────────────────
        AddSpacer(row.transform, 6f);

        // ── Name + version ────────────────────────────────────────────────────
        var labelGo  = new GameObject("Label");
        labelGo.transform.SetParent(row.transform, false);
        var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
        labelTmp.font             = Style.Font;
        labelTmp.fontSize         = Style.FontSizeBody;
        labelTmp.color            = enabled ? Style.ColorText : Style.ColorTextMuted;
        labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
        labelTmp.overflowMode     = TextOverflowModes.Ellipsis;
        labelTmp.alignment        = TextAlignmentOptions.MidlineLeft;
        string mHex = ColorUtility.ToHtmlStringRGB(Style.ColorTextMuted);
        labelTmp.text = string.IsNullOrWhiteSpace(d.About.Version)
            ? d.EffectiveName
            : $"{d.EffectiveName}  <size={Style.FontSizeSmall:0}><color=#{mHex}>{d.About.Version}</color></size>";
        labelGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // ── Spacer ────────────────────────────────────────────────────────────
        AddSpacer(row.transform, 8f);

        // ── Toggle pill ───────────────────────────────────────────────────────
        //    Image.color = WHITE so the ColorBlock colors are the actual visible
        //    colors (Unity multiplies Image.color * ColorBlock.currentColor).
        var togGo  = new GameObject("TogglePill");
        togGo.transform.SetParent(row.transform, false);
        var togImg = togGo.AddComponent<Image>();
        togImg.color = Color.white;                       // ← base white; CB does the tinting
        togImg.raycastTarget = true;
        var togLe  = togGo.AddComponent<LayoutElement>();
        togLe.minWidth       = 66f;
        togLe.preferredWidth = 66f;
        togLe.flexibleWidth  = 0f;
        togLe.minHeight      = 28f;
        togLe.preferredHeight = 28f;

        var symTmp = MakeChildTmp(togGo, Style);
        symTmp.text      = TogglePillText(enabled);
        symTmp.fontSize  = Style.FontSizeSmall;
        symTmp.color     = ToggleSymColor(enabled);
        symTmp.fontStyle = FontStyles.Bold;
        symTmp.alignment = TextAlignmentOptions.Center;
        symTmp.raycastTarget = false;

        var togBtn = togGo.AddComponent<Button>();
        togBtn.targetGraphic = togImg;
        togBtn.transition    = Selectable.Transition.ColorTint;
        var bc               = ColorBlock.defaultColorBlock;
        bc.normalColor       = ToggleBgColor(enabled);
        bc.highlightedColor  = ToggleBgHover(enabled);
        bc.pressedColor      = ToggleBgPressed(enabled);
        bc.fadeDuration      = 0.08f;
        togBtn.colors        = bc;

        // ── Trailing pad ──────────────────────────────────────────────────────
        AddSpacer(row.transform, 8f);

        // ── ModRowItem ────────────────────────────────────────────────────────
        var item        = row.AddComponent<ModRowItem>();
        item.ModId      = d.EffectiveModId;
        item.Index      = idx;
        item.Controller = this;
        item.RowBg      = rowBg;
        item.AccentImg  = null!;
        item.NameTmp    = labelTmp;
        item.ToggleImg  = togImg;
        item.SymbolTmp  = symTmp;
        item.IsEnabled  = enabled;

        drag.RowItem    = item;
        drag.Controller = this;

        togBtn.onClick.AddListener(() => item.ToggleEnabled(Style));

        return item;
    }

    // ── Row helpers ────────────────────────────────────────────────────────────

    internal static Color RowBgColor(bool enabled, bool selected)
    {
        if (selected)
            return new Color(0.16f, 0.30f, 0.52f, 1f);
        return enabled
            ? new Color(0.14f, 0.17f, 0.22f, 0.9f)
            : new Color(0.10f, 0.10f, 0.12f, 0.7f);
    }

    internal static string TogglePillText(bool enabled) =>
        enabled ? "ON" : "OFF";

    /// Creates a TMP child that stretches to fill the parent, using the game's font.
    private static TextMeshProUGUI MakeChildTmp(GameObject parent, GameUiStyle style)
    {
        var go  = new GameObject("Txt");
        go.transform.SetParent(parent.transform, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = style.Font;
        return tmp;
    }

    private static void AddSpacer(Transform parent, float width)
    {
        var go = new GameObject("_");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f;
    }

    private void RefreshInfoPane()
    {
        bool hasSelection = _selectedIndex >= 0 && _selectedIndex < Mods.Count;
        InfoPlaceholderGo.SetActive(!hasSelection);
        InfoContentGo.SetActive(hasSelection);

        if (!hasSelection) return;

        var d = Mods[_selectedIndex];

        InfoNameTmp.text = d.EffectiveName;

        var detail = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(d.About.Author))
            detail.AppendLine($"Author:  {d.About.Author}");
        if (!string.IsNullOrWhiteSpace(d.About.Version))
            detail.AppendLine($"Version: {d.About.Version}");
        detail.AppendLine($"ID:      {d.EffectiveModId}");
        InfoDetailTmp.text = detail.ToString().TrimEnd();

        var desc = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(d.About.Description))
            desc.Append(BbCodeToTmp.Convert(d.About.Description));
        if (!string.IsNullOrWhiteSpace(d.About.InGameDescription))
        {
            if (desc.Length > 0) desc.AppendLine();
            desc.Append(BbCodeToTmp.Convert(d.About.InGameDescription));
        }

        if (d.About.Tags?.Count > 0)
        {
            if (desc.Length > 0) desc.AppendLine("\n");
            desc.Append($"<size=80%><color=#aab8cc>Tags: {string.Join(", ", d.About.Tags)}</color></size>");
        }
        if (d.About.DependsOn?.Count > 0)
        {
            var ids = d.About.DependsOn.Where(r => r.IsValid).Select(r => r.ModID!);
            if (desc.Length > 0) desc.AppendLine();
            desc.Append($"<size=80%><color=#ffb347>Requires: {string.Join(", ", ids)}</color></size>");
        }

        InfoDescTmp.text = desc.Length > 0 ? desc.ToString().Trim() : "<color=#667>No description provided.</color>";
    }

    // ── Static helpers ─────────────────────────────────────────────────────────

    internal static Color AccentColor(bool enabled) =>
        enabled ? new Color(0.22f, 0.68f, 0.52f, 0.9f) : new Color(0.38f, 0.38f, 0.40f, 0.3f);

    internal static Color ToggleBgColor(bool enabled) =>
        enabled ? new Color(0.14f, 0.48f, 0.32f, 1f) : new Color(0.24f, 0.24f, 0.27f, 0.9f);

    internal static Color ToggleBgHover(bool enabled) =>
        enabled ? new Color(0.18f, 0.56f, 0.38f, 1f) : new Color(0.32f, 0.32f, 0.36f, 1f);

    internal static Color ToggleBgPressed(bool enabled) =>
        enabled ? new Color(0.10f, 0.38f, 0.24f, 1f) : new Color(0.18f, 0.18f, 0.20f, 1f);

    internal static Color ToggleSymColor(bool enabled) =>
        enabled ? new Color(0.55f, 1f, 0.72f, 1f) : new Color(0.58f, 0.58f, 0.62f, 0.8f);

    private static void AddFixedLayout(GameObject go, float w, float h)
    {
        var le           = go.AddComponent<LayoutElement>();
        le.minWidth      = w; le.preferredWidth  = w;
        le.minHeight     = h; le.preferredHeight = h;
        le.flexibleWidth = 0f; le.flexibleHeight  = 0f;
    }

}

// ─────────────────────────────────────────────────────────────────────────────
// ModRowItem  – per-row state + click selection
// ─────────────────────────────────────────────────────────────────────────────
internal sealed class ModRowItem : MonoBehaviour, IPointerClickHandler
{
    internal string              ModId      = "";
    internal int                 Index;
    internal ModListController   Controller = null!;
    internal Image               RowBg      = null!;
    internal Image               AccentImg  = null!;
    internal TextMeshProUGUI     NameTmp    = null!;
    internal Image               ToggleImg  = null!;
    internal TextMeshProUGUI     SymbolTmp  = null!;
    internal bool                IsEnabled;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!eventData.dragging)
            Controller.SelectMod(Index);
    }

    internal void SetSelected(bool selected, GameUiStyle style)
    {
        if (RowBg == null) return;
        RowBg.color = ModListController.RowBgColor(IsEnabled, selected);
    }

    internal void ToggleEnabled(GameUiStyle style)
    {
        IsEnabled = !IsEnabled;
        ModRegistry.SetEnabled(ModId, IsEnabled, ModulusModLoaderPlugin.Log);

        if (AccentImg != null) AccentImg.color = ModListController.AccentColor(IsEnabled);
        NameTmp.color  = IsEnabled ? style.ColorText : style.ColorTextMuted;
        SymbolTmp.text  = ModListController.TogglePillText(IsEnabled);
        SymbolTmp.color = ModListController.ToggleSymColor(IsEnabled);
        RowBg.color     = ModListController.RowBgColor(IsEnabled, false);

        // Image stays white; only the ColorBlock changes the visible pill color
        var togBtn = ToggleImg.GetComponent<Button>();
        if (togBtn != null)
        {
            var bc              = togBtn.colors;
            bc.normalColor      = ModListController.ToggleBgColor(IsEnabled);
            bc.highlightedColor = ModListController.ToggleBgHover(IsEnabled);
            bc.pressedColor     = ModListController.ToggleBgPressed(IsEnabled);
            togBtn.colors       = bc;
        }

        ModulusModLoaderPlugin.Log?.LogInfo(
            $"[ModsMenu] {ModId} → {(IsEnabled ? "enabled" : "disabled")} (restart to apply)");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ModRowDragHandler  – drag handle, reorders items in ModListController
// ─────────────────────────────────────────────────────────────────────────────
internal sealed class ModRowDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    internal ModListController Controller = null!;
    internal ModRowItem        RowItem    = null!;

    private GameObject?    _ghost;
    private RectTransform? _canvasRt;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _canvasRt = Controller.OverlayCanvas.GetComponent<RectTransform>();
        Controller.ScrollRect.enabled = false;
        Controller.OnDragBegin(RowItem.Index);

        // Ghost: thin label-only copy floating over canvas
        _ghost = new GameObject("DragGhost");
        _ghost.transform.SetParent(_canvasRt, false);

        var ghostImg = _ghost.AddComponent<Image>();
        var p = Controller.Style.ColorPanelBg;
        ghostImg.color = new Color(p.r * 1.3f, p.g * 1.3f, p.b * 1.4f, 0.92f);
        if (Controller.Style.PanelSprite != null)
        {
            ghostImg.sprite = Controller.Style.PanelSprite;
            ghostImg.type   = Image.Type.Sliced;
        }

        var ghostRt       = _ghost.GetComponent<RectTransform>();
        ghostRt.sizeDelta = new Vector2(300f, 40f);
        ghostRt.pivot     = new Vector2(0f, 0.5f);

        var lbl = new GameObject("Txt");
        lbl.transform.SetParent(_ghost.transform, false);
        var lblRt     = lbl.AddComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(10f, 0f); lblRt.offsetMax = Vector2.zero;
        var lblTmp    = lbl.AddComponent<TextMeshProUGUI>();
        lblTmp.font   = Controller.Style.Font;
        lblTmp.fontSize = Controller.Style.FontSizeBody;
        lblTmp.color  = Controller.Style.ColorText;
        int mi        = RowItem.Index;
        lblTmp.text   = mi >= 0 && mi < Controller.Mods.Count
                      ? Controller.Mods[mi].EffectiveName : "";
        lblTmp.alignment = TextAlignmentOptions.MidlineLeft;
        lblTmp.raycastTarget = false;

        MoveGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveGhost(eventData);
        int idx = Controller.GetDropIndex(eventData.position, eventData.pressEventCamera);
        Controller.ShowDropIndicator(idx);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Controller.ScrollRect.enabled = true;
        int to = Controller.GetDropIndex(eventData.position, eventData.pressEventCamera);
        Controller.MoveItem(RowItem.Index, to);
        Controller.HideDropIndicator();

        if (_ghost != null) { Object.Destroy(_ghost); _ghost = null; }
    }

    private void MoveGhost(PointerEventData eventData)
    {
        if (_ghost == null || _canvasRt == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRt, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            _ghost.GetComponent<RectTransform>().anchoredPosition = local;
        }
    }
}
