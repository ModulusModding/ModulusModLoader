using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModulusModLoader.Config;

/// <summary>
/// Builds vanilla-styled UI controls for a mod's BepInEx <see cref="ConfigFile"/>.
/// Mod authors do not need to call this directly - the loader's mod menu calls
/// <see cref="Populate"/> automatically when a mod is selected. Each control writes
/// changes back to the live <see cref="ConfigEntryBase"/> (BepInEx auto-saves to disk).
/// </summary>
internal static class ModConfigPanel
{
    private static readonly Color SectionTextColor = new(0.78f, 0.86f, 0.98f);

    /// <summary>
    /// Removes any prior controls and rebuilds the panel for the mod identified by
    /// <paramref name="modId"/> (BepInEx GUID or About.xml ModID). When
    /// <paramref name="rootPath"/> is provided, plugins whose DLL lives inside that
    /// folder are also matched - this handles mods whose About.xml ModID differs from
    /// the BepInPlugin GUID.
    /// </summary>
    internal static void Populate(Transform host, string modId, GameUiStyle style, string? rootPath = null)
    {
        if (host == null) return;
        ClearChildren(host);

        List<ConfigFile> configs = ResolveConfigs(modId, rootPath);
        if (configs.Count == 0 || configs.All(c => !c.Any()))
        {
            AddNote(host, "No configurable settings exposed by this mod.", style);
            return;
        }

        IEnumerable<IGrouping<string, ConfigEntryBase>> sections = configs
            .SelectMany(c => c.Select(kv => kv.Value))
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Definition.Section) ? "General" : e.Definition.Section)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, ConfigEntryBase> section in sections)
        {
            BuildSectionHeader(host, section.Key, style);
            foreach (ConfigEntryBase entry in section.OrderBy(e => e.Definition.Key, StringComparer.OrdinalIgnoreCase))
                BuildRow(host, entry, style);
        }
    }

    private static List<ConfigFile> ResolveConfigs(string modId, string? rootPath)
    {
        var found = new List<ConfigFile>();
        var seen  = new HashSet<ConfigFile>();

        try
        {
            // 1. Direct match by plugin GUID.
            if (!string.IsNullOrWhiteSpace(modId) &&
                Chainloader.PluginInfos.TryGetValue(modId, out var info) &&
                info?.Instance != null &&
                seen.Add(info.Instance.Config))
            {
                found.Add(info.Instance.Config);
            }

            // 2. Case-insensitive GUID scan.
            if (!string.IsNullOrWhiteSpace(modId))
            {
                foreach (var pair in Chainloader.PluginInfos)
                {
                    if (pair.Value?.Instance == null) continue;
                    if (string.Equals(pair.Key, modId, StringComparison.OrdinalIgnoreCase) &&
                        seen.Add(pair.Value.Instance.Config))
                    {
                        found.Add(pair.Value.Instance.Config);
                    }
                }
            }

            // 3. Folder-based match: plugins whose DLL lives inside the mod's root path.
            //    Handles mods whose About.xml ModID differs from the BepInPlugin GUID,
            //    and mods that ship multiple plugin DLLs in one folder.
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                string normalizedRoot = NormalizeFolder(rootPath!);
                foreach (var pair in Chainloader.PluginInfos)
                {
                    var inst = pair.Value?.Instance;
                    if (inst == null) continue;
                    string? loc = pair.Value?.Location;
                    if (string.IsNullOrEmpty(loc)) continue;
                    string normalizedLoc = NormalizeFolder(Path.GetDirectoryName(loc!) ?? string.Empty);
                    if (normalizedLoc.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
                        seen.Add(inst.Config))
                    {
                        found.Add(inst.Config);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogWarning($"ModConfigPanel: failed to resolve config for {modId}: {ex.Message}");
        }
        return found;
    }

    private static string NormalizeFolder(string path)
    {
        try
        {
            string full = Path.GetFullPath(path).Replace('/', Path.DirectorySeparatorChar);
            return full.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }
        catch
        {
            return path;
        }
    }

    private static void ClearChildren(Transform host)
    {
        for (int i = host.childCount - 1; i >= 0; i--)
            Object.Destroy(host.GetChild(i).gameObject);
    }

    private static void BuildSectionHeader(Transform host, string name, GameUiStyle style)
    {
        GameObject go = new("CfgSection");
        go.transform.SetParent(host, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font     = style.Font;
        tmp.fontSize = style.FontSizeSmall * 1.05f;
        tmp.color    = SectionTextColor;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.text     = name;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        go.AddComponent<LayoutElement>().minHeight = style.FontSizeSmall * 2f;
    }

    private static void AddNote(Transform host, string text, GameUiStyle style)
    {
        GameObject go = new("CfgNote");
        go.transform.SetParent(host, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font  = style.Font;
        tmp.fontSize = style.FontSizeSmall;
        tmp.color = style.ColorTextMuted;
        tmp.text  = text;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        go.AddComponent<LayoutElement>().minHeight = style.FontSizeSmall * 1.6f;
    }

    private static void BuildRow(Transform host, ConfigEntryBase entry, GameUiStyle style)
    {
        // Outer block: vertical (header row + optional wrapping description below).
        GameObject block = new("CfgEntry");
        block.transform.SetParent(host, false);
        Image bg = block.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.025f);

        VerticalLayoutGroup vlg = block.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                 = 2f;
        vlg.padding                 = new RectOffset(10, 10, 6, 6);
        vlg.childAlignment          = TextAnchor.UpperLeft;
        vlg.childControlHeight      = true;
        vlg.childControlWidth       = true;
        vlg.childForceExpandHeight  = false;
        vlg.childForceExpandWidth   = true;

        ContentSizeFitter csf = block.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Header row: name on the left, editor on the right.
        GameObject row = new("Header");
        row.transform.SetParent(block.transform, false);
        Image rowBg = row.AddComponent<Image>(); rowBg.color = Color.clear;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing             = 10f;
        hlg.childAlignment      = TextAnchor.MiddleLeft;
        hlg.childControlHeight  = true;
        hlg.childControlWidth   = true;
        hlg.childForceExpandHeight = false;
        hlg.childForceExpandWidth  = false;

        LayoutElement rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 30f;

        GameObject lblGo = new("Lbl");
        lblGo.transform.SetParent(row.transform, false);
        TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.font      = style.Font;
        lbl.fontSize  = style.FontSizeSmall;
        lbl.color     = style.ColorText;
        lbl.text      = entry.Definition.Key;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        lbl.fontStyle = FontStyles.Bold;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        lbl.overflowMode     = TextOverflowModes.Ellipsis;
        LayoutElement lblLe = lblGo.AddComponent<LayoutElement>();
        lblLe.flexibleWidth = 1f;
        lblLe.minWidth      = 100f;

        // Editor based on type (added as a sibling of the label inside the header row).
        Type t = entry.SettingType;
        if (t == typeof(bool))
            BuildToggle(row.transform, entry, style);
        else if (t.IsEnum)
            BuildEnumDropdown(row.transform, entry, t, style);
        else if (entry.Description?.AcceptableValues is AcceptableValueList<string> sList)
            BuildStringDropdown(row.transform, entry, sList.AcceptableValues, style);
        else if (t == typeof(int) && entry.Description?.AcceptableValues is AcceptableValueRange<int> iRange)
            BuildIntSlider(row.transform, entry, iRange, style);
        else if (t == typeof(float) && entry.Description?.AcceptableValues is AcceptableValueRange<float> fRange)
            BuildFloatSlider(row.transform, entry, fRange, style);
        else if (IsNumber(t))
            BuildNumberInput(row.transform, entry, style);
        else
            BuildTextInput(row.transform, entry, style);

        // Description: wraps below the header row using the full width; muted, smaller.
        string? desc = entry.Description?.Description;
        if (!string.IsNullOrWhiteSpace(desc))
        {
            GameObject descGo = new("Desc");
            descGo.transform.SetParent(block.transform, false);
            TextMeshProUGUI descTmp = descGo.AddComponent<TextMeshProUGUI>();
            descTmp.font             = style.Font;
            descTmp.fontSize         = style.FontSizeSmall * 0.85f;
            descTmp.color            = style.ColorTextMuted;
            descTmp.text             = desc!.Trim();
            descTmp.alignment        = TextAlignmentOptions.TopLeft;
            descTmp.textWrappingMode = TextWrappingModes.Normal;
            descTmp.overflowMode     = TextOverflowModes.Overflow;
            descGo.AddComponent<LayoutElement>();
        }
    }

    private static bool IsNumber(Type t) =>
        t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
        t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) ||
        t == typeof(float) || t == typeof(double) || t == typeof(decimal);

    // ── Editors ─────────────────────────────────────────────────────────────

    private static void BuildToggle(Transform parent, ConfigEntryBase entry, GameUiStyle style)
    {
        GameObject go = new("CfgToggle");
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = Color.white;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = 56f; le.preferredWidth = 56f; le.flexibleWidth = 0f;
        le.minHeight = 26f; le.preferredHeight = 26f;

        bool current = (bool)entry.BoxedValue;

        TextMeshProUGUI sym = MakeChildLabel(go.transform, style);
        sym.text = current ? "ON" : "OFF";
        sym.fontStyle = FontStyles.Bold;
        sym.color = current ? new Color(0.55f, 1f, 0.72f) : new Color(0.6f, 0.6f, 0.62f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition    = Selectable.Transition.ColorTint;
        btn.colors        = ToggleColors(current);
        btn.onClick.AddListener(() =>
        {
            current = !current;
            entry.BoxedValue = current;
            sym.text  = current ? "ON" : "OFF";
            sym.color = current ? new Color(0.55f, 1f, 0.72f) : new Color(0.6f, 0.6f, 0.62f);
            btn.colors = ToggleColors(current);
        });
    }

    private static ColorBlock ToggleColors(bool on)
    {
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = on ? new Color(0.14f, 0.48f, 0.32f, 1f) : new Color(0.24f, 0.24f, 0.27f, 0.9f);
        cb.highlightedColor = on ? new Color(0.18f, 0.56f, 0.38f, 1f) : new Color(0.32f, 0.32f, 0.36f, 1f);
        cb.pressedColor     = on ? new Color(0.10f, 0.38f, 0.24f, 1f) : new Color(0.18f, 0.18f, 0.20f, 1f);
        cb.fadeDuration     = 0.08f;
        return cb;
    }

    private static void BuildEnumDropdown(Transform parent, ConfigEntryBase entry, Type enumType, GameUiStyle style)
    {
        string[] names = Enum.GetNames(enumType);
        TMP_Dropdown dd = MakeDropdown(parent, names, names.ToList().IndexOf(entry.BoxedValue?.ToString() ?? string.Empty), style);
        dd.onValueChanged.AddListener(idx =>
        {
            try { entry.BoxedValue = Enum.Parse(enumType, names[idx]); }
            catch (Exception ex) { ModulusModLoaderPlugin.Log?.LogWarning($"ModConfigPanel: enum set failed: {ex.Message}"); }
        });
    }

    private static void BuildStringDropdown(Transform parent, ConfigEntryBase entry, string[] options, GameUiStyle style)
    {
        int idx = Array.IndexOf(options, entry.BoxedValue?.ToString() ?? string.Empty);
        TMP_Dropdown dd = MakeDropdown(parent, options, idx, style);
        dd.onValueChanged.AddListener(i => entry.BoxedValue = options[i]);
    }

    private static TMP_Dropdown MakeDropdown(Transform parent, IList<string> options, int selected, GameUiStyle style)
    {
        GameObject go = new("CfgDD");
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.08f);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = 160f; le.preferredWidth = 220f; le.flexibleWidth = 0f;
        le.minHeight = 28f; le.preferredHeight = 28f;

        TMP_Dropdown dd = go.AddComponent<TMP_Dropdown>();
        dd.targetGraphic = img;

        // Caption text
        GameObject capGo = new("Caption");
        capGo.transform.SetParent(go.transform, false);
        RectTransform capRt = capGo.AddComponent<RectTransform>();
        capRt.anchorMin = Vector2.zero; capRt.anchorMax = Vector2.one;
        capRt.offsetMin = new Vector2(8, 2); capRt.offsetMax = new Vector2(-22, -2);
        TextMeshProUGUI cap = capGo.AddComponent<TextMeshProUGUI>();
        cap.font = style.Font; cap.fontSize = style.FontSizeSmall;
        cap.color = style.ColorText; cap.alignment = TextAlignmentOptions.MidlineLeft;
        cap.raycastTarget = false;
        dd.captionText = cap;

        // Item template (minimal)
        GameObject template = new("Template");
        template.transform.SetParent(go.transform, false);
        RectTransform tplRt = template.AddComponent<RectTransform>();
        tplRt.anchorMin = new Vector2(0, 0); tplRt.anchorMax = new Vector2(1, 0);
        tplRt.pivot     = new Vector2(0.5f, 1f);
        tplRt.sizeDelta = new Vector2(0, 150);
        Image tplBg = template.AddComponent<Image>();
        tplBg.color = new Color(0.08f, 0.10f, 0.13f, 0.97f);
        ScrollRect sr = template.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        GameObject vp = new("Viewport");
        vp.transform.SetParent(template.transform, false);
        RectTransform vpRt = vp.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
        vp.AddComponent<RectMask2D>();
        vp.AddComponent<Image>().color = Color.clear;

        GameObject content = new("Content");
        content.transform.SetParent(vp.transform, false);
        RectTransform contRt = content.AddComponent<RectTransform>();
        contRt.anchorMin = new Vector2(0, 1); contRt.anchorMax = new Vector2(1, 1);
        contRt.pivot     = new Vector2(0.5f, 1f);
        contRt.sizeDelta = new Vector2(0, 28);

        GameObject item = new("Item");
        item.transform.SetParent(content.transform, false);
        RectTransform itemRt = item.AddComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0, 0.5f); itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.sizeDelta = new Vector2(0, 26);
        Toggle itemTog = item.AddComponent<Toggle>();

        GameObject itemBg = new("ItemBg");
        itemBg.transform.SetParent(item.transform, false);
        RectTransform itemBgRt = itemBg.AddComponent<RectTransform>();
        itemBgRt.anchorMin = Vector2.zero; itemBgRt.anchorMax = Vector2.one;
        itemBgRt.offsetMin = Vector2.zero; itemBgRt.offsetMax = Vector2.zero;
        Image itemBgImg = itemBg.AddComponent<Image>();
        itemBgImg.color = new Color(0.18f, 0.32f, 0.5f, 0.6f);
        itemTog.targetGraphic = itemBgImg;

        GameObject itemCheck = new("Check");
        itemCheck.transform.SetParent(item.transform, false);
        RectTransform itemCheckRt = itemCheck.AddComponent<RectTransform>();
        itemCheckRt.anchorMin = Vector2.zero; itemCheckRt.anchorMax = Vector2.one;
        itemCheckRt.offsetMin = Vector2.zero; itemCheckRt.offsetMax = Vector2.zero;
        Image itemCheckImg = itemCheck.AddComponent<Image>();
        itemCheckImg.color = new Color(0.22f, 0.55f, 0.85f, 0.9f);
        itemTog.graphic = itemCheckImg;

        GameObject itemLabelGo = new("ItemLabel");
        itemLabelGo.transform.SetParent(item.transform, false);
        RectTransform ilRt = itemLabelGo.AddComponent<RectTransform>();
        ilRt.anchorMin = Vector2.zero; ilRt.anchorMax = Vector2.one;
        ilRt.offsetMin = new Vector2(8, 1); ilRt.offsetMax = new Vector2(-8, -1);
        TextMeshProUGUI itemLabel = itemLabelGo.AddComponent<TextMeshProUGUI>();
        itemLabel.font = style.Font; itemLabel.fontSize = style.FontSizeSmall;
        itemLabel.color = style.ColorText; itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
        itemLabel.raycastTarget = false;

        sr.viewport = vpRt; sr.content = contRt;
        dd.template = tplRt;
        dd.itemText = itemLabel;
        template.SetActive(false);

        dd.options = options.Select(o => new TMP_Dropdown.OptionData(o)).ToList();
        if (selected < 0) selected = 0;
        dd.value = selected;
        dd.RefreshShownValue();

        // Arrow caret
        GameObject arrow = new("Arrow");
        arrow.transform.SetParent(go.transform, false);
        RectTransform arrowRt = arrow.AddComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(1, 0.5f); arrowRt.anchorMax = new Vector2(1, 0.5f);
        arrowRt.pivot = new Vector2(1, 0.5f); arrowRt.sizeDelta = new Vector2(18, 18);
        arrowRt.anchoredPosition = new Vector2(-6, 0);
        TextMeshProUGUI arrowTmp = arrow.AddComponent<TextMeshProUGUI>();
        arrowTmp.font = style.Font; arrowTmp.fontSize = style.FontSizeSmall;
        arrowTmp.color = style.ColorTextMuted;
        arrowTmp.text = "▼";
        arrowTmp.alignment = TextAlignmentOptions.Center;
        arrowTmp.raycastTarget = false;

        return dd;
    }

    private static void BuildIntSlider(Transform parent, ConfigEntryBase entry, AcceptableValueRange<int> range, GameUiStyle style)
    {
        GameObject group = MakeEditorGroup(parent);
        Slider s = MakeSlider(group.transform, range.MinValue, range.MaxValue, (int)entry.BoxedValue, style, wholeNumbers: true);
        TextMeshProUGUI val = MakeValueLabel(group.transform, ((int)entry.BoxedValue).ToString(CultureInfo.InvariantCulture), style);
        s.onValueChanged.AddListener(v =>
        {
            int iv = Mathf.RoundToInt(v);
            entry.BoxedValue = iv;
            val.text = iv.ToString(CultureInfo.InvariantCulture);
        });
    }

    private static void BuildFloatSlider(Transform parent, ConfigEntryBase entry, AcceptableValueRange<float> range, GameUiStyle style)
    {
        GameObject group = MakeEditorGroup(parent);
        Slider s = MakeSlider(group.transform, range.MinValue, range.MaxValue, (float)entry.BoxedValue, style, wholeNumbers: false);
        TextMeshProUGUI val = MakeValueLabel(group.transform, ((float)entry.BoxedValue).ToString("0.##", CultureInfo.InvariantCulture), style);
        s.onValueChanged.AddListener(v =>
        {
            entry.BoxedValue = v;
            val.text = v.ToString("0.##", CultureInfo.InvariantCulture);
        });
    }

    private static GameObject MakeEditorGroup(Transform parent)
    {
        GameObject go = new("EditGroup");
        go.transform.SetParent(parent, false);
        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlHeight = true; h.childControlWidth = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = 200f; le.preferredWidth = 240f; le.flexibleWidth = 0f;
        le.minHeight = 26f; le.preferredHeight = 26f;
        return go;
    }

    private static TextMeshProUGUI MakeValueLabel(Transform parent, string initial, GameUiStyle style)
    {
        GameObject go = new("Val");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = style.Font; tmp.fontSize = style.FontSizeSmall;
        tmp.color = style.ColorTextMuted;
        tmp.text = initial;
        tmp.alignment = TextAlignmentOptions.MidlineRight;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = 48f; le.preferredWidth = 48f; le.flexibleWidth = 0f;
        return tmp;
    }

    private static Slider MakeSlider(Transform parent, float min, float max, float value, GameUiStyle style, bool wholeNumbers)
    {
        GameObject go = new("Slider");
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = 140f; le.preferredWidth = 180f; le.flexibleWidth = 1f;
        le.minHeight = 22f; le.preferredHeight = 22f;

        GameObject bgGo = new("Bg");
        bgGo.transform.SetParent(go.transform, false);
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.5f); bgRt.anchorMax = new Vector2(1, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f); bgRt.sizeDelta = new Vector2(0, 6);
        Image bg = bgGo.AddComponent<Image>(); bg.color = new Color(1, 1, 1, 0.10f);

        GameObject fillArea = new("FillArea");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillRt = fillArea.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0, 0.5f); fillRt.anchorMax = new Vector2(1, 0.5f);
        fillRt.pivot = new Vector2(0.5f, 0.5f); fillRt.sizeDelta = new Vector2(-12, 6);
        GameObject fill = new("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillIRt = fill.AddComponent<RectTransform>();
        fillIRt.anchorMin = Vector2.zero; fillIRt.anchorMax = Vector2.one;
        fillIRt.offsetMin = Vector2.zero; fillIRt.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.22f, 0.55f, 0.85f, 0.9f);

        GameObject handleArea = new("HandleArea");
        handleArea.transform.SetParent(go.transform, false);
        RectTransform haRt = handleArea.AddComponent<RectTransform>();
        haRt.anchorMin = new Vector2(0, 0); haRt.anchorMax = new Vector2(1, 1);
        haRt.offsetMin = new Vector2(8, 0); haRt.offsetMax = new Vector2(-8, 0);
        GameObject handle = new("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRt = handle.AddComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(14, 14);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = style.ColorText;

        Slider s = go.AddComponent<Slider>();
        s.targetGraphic = handleImg;
        s.fillRect = fillIRt;
        s.handleRect = handleRt;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min; s.maxValue = max;
        s.wholeNumbers = wholeNumbers;
        s.value = value;
        return s;
    }

    private static void BuildNumberInput(Transform parent, ConfigEntryBase entry, GameUiStyle style)
    {
        TMP_InputField input = MakeInput(parent, Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture) ?? string.Empty, style);
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.onEndEdit.AddListener(text =>
        {
            try
            {
                object converted = Convert.ChangeType(text, entry.SettingType, CultureInfo.InvariantCulture);
                entry.BoxedValue = converted;
            }
            catch (Exception) { input.text = Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture) ?? string.Empty; }
        });
    }

    private static void BuildTextInput(Transform parent, ConfigEntryBase entry, GameUiStyle style)
    {
        TMP_InputField input = MakeInput(parent, entry.BoxedValue?.ToString() ?? string.Empty, style);
        input.onEndEdit.AddListener(text =>
        {
            try
            {
                object? converted = entry.SettingType == typeof(string)
                    ? text
                    : Convert.ChangeType(text, entry.SettingType, CultureInfo.InvariantCulture);
                entry.BoxedValue = converted!;
            }
            catch (Exception) { input.text = entry.BoxedValue?.ToString() ?? string.Empty; }
        });
    }

    private static TMP_InputField MakeInput(Transform parent, string initial, GameUiStyle style)
    {
        GameObject go = new("CfgInput");
        go.transform.SetParent(parent, false);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = 160f; le.preferredWidth = 220f; le.flexibleWidth = 0f;
        le.minHeight = 26f; le.preferredHeight = 26f;

        GameObject txtArea = new("TextArea");
        txtArea.transform.SetParent(go.transform, false);
        RectTransform taRt = txtArea.AddComponent<RectTransform>();
        taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
        taRt.offsetMin = new Vector2(8, 2); taRt.offsetMax = new Vector2(-8, -2);
        txtArea.AddComponent<RectMask2D>();

        GameObject txtGo = new("Text");
        txtGo.transform.SetParent(txtArea.transform, false);
        RectTransform txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.font = style.Font; tmp.fontSize = style.FontSizeSmall;
        tmp.color = style.ColorText;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_InputField input = go.AddComponent<TMP_InputField>();
        input.targetGraphic     = bg;
        input.textViewport      = taRt;
        input.textComponent     = tmp;
        input.fontAsset         = style.Font;
        input.pointSize         = style.FontSizeSmall;
        input.lineType          = TMP_InputField.LineType.SingleLine;
        input.customCaretColor  = true;
        input.caretColor        = style.ColorText;
        input.caretWidth        = 2;
        input.caretBlinkRate    = 0.85f;
        input.selectionColor    = new Color(0.22f, 0.55f, 0.85f, 0.5f);
        input.shouldHideMobileInput = true;
        input.text = initial;
        return input;
    }

    private static TextMeshProUGUI MakeChildLabel(Transform parent, GameUiStyle style)
    {
        GameObject go = new("Lbl");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = style.Font;
        tmp.fontSize = style.FontSizeSmall;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }
}
