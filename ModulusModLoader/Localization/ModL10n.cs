using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ModulusModLoader.Localization;

/// <summary>
/// Per-mod localization. Each mod ships a folder of language files (e.g. <c>Localization/en.json</c>,
/// <c>Localization/de.json</c>); look strings up by key with <see cref="Get(string,string,string?)"/>.
///
/// Files are JSON dictionaries (<c>"key": "value"</c>) or nested JSON objects (dot-keys join nested paths).
/// Missing keys fall back to (1) the configured fallback language, (2) the supplied <c>fallback</c>
/// argument, (3) the literal key. The catalog reloads automatically when the player switches language.
/// </summary>
public static class ModL10n
{
    /// <summary>Default folder name (under the mod's distribution root) that holds the language files.</summary>
    public const string DefaultFolderName = "Localization";

    private const string DefaultFallback = "en";

    private static readonly object Sync = new();
    private static readonly ConcurrentDictionary<string, Catalog> Catalogs =
        new(StringComparer.OrdinalIgnoreCase);

    private static string _currentLanguage = "en";
    private static string _fallbackLanguage = DefaultFallback;
    private static bool _hookInstalled;

    /// <summary>
    /// Two-letter (or "xx-yy") code of the active in-game language. Updated whenever the
    /// game raises <c>LocalizationUtility.OnLanguageUpdate</c>.
    /// </summary>
    public static string CurrentLanguage => _currentLanguage;

    /// <summary>Fires after <see cref="CurrentLanguage"/> changes and catalogs are reloaded.</summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// Override the fallback language when a key is missing from the active language. Defaults to <c>en</c>.
    /// </summary>
    public static void SetFallbackLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return;
        _fallbackLanguage = NormalizeCode(languageCode);
    }

    /// <summary>
    /// Register a folder of language files for <paramref name="modId"/>.
    /// Pass the mod's distribution root - the loader resolves <c>{root}/{folderName}</c>.
    /// Re-registering replaces the previous folder for the same mod.
    /// </summary>
    public static void Register(string modId, string distributionFolder, string folderName = DefaultFolderName)
    {
        if (string.IsNullOrWhiteSpace(modId)) throw new ArgumentException("modId is required.", nameof(modId));
        if (string.IsNullOrWhiteSpace(distributionFolder))
            throw new ArgumentException("distributionFolder is required.", nameof(distributionFolder));

        EnsureLanguageHook();

        string root = Path.Combine(distributionFolder, folderName);
        Catalog catalog = new(modId, root);
        Catalogs[modId] = catalog;
        catalog.Reload(_currentLanguage, _fallbackLanguage);
    }

    /// <summary>Returns the localized string for <paramref name="key"/> (or <paramref name="fallback"/>/key when missing).</summary>
    public static string Get(string modId, string key, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        if (Catalogs.TryGetValue(modId, out Catalog? cat) && cat.TryGet(key, out string? text))
            return text;
        return fallback ?? key;
    }

    /// <summary>Same as <see cref="Get(string,string,string?)"/> but applies <c>string.Format</c> with <paramref name="args"/>.</summary>
    public static string Format(string modId, string key, params object[] args)
    {
        string template = Get(modId, key);
        if (args == null || args.Length == 0) return template;
        try { return string.Format(CultureInfo.CurrentCulture, template, args); }
        catch (FormatException) { return template; }
    }

    /// <summary>Returns true when <paramref name="key"/> exists in the active or fallback language.</summary>
    public static bool Has(string modId, string key) =>
        Catalogs.TryGetValue(modId, out Catalog? cat) && cat.TryGet(key, out _);

    /// <summary>Force a reload of every registered mod's catalog (useful when files change on disk).</summary>
    public static void ReloadAll()
    {
        foreach (Catalog catalog in Catalogs.Values)
            catalog.Reload(_currentLanguage, _fallbackLanguage);
    }

    internal static void HandleLanguageUpdate(string newLanguage)
    {
        string normalized = NormalizeCode(newLanguage);
        if (string.Equals(_currentLanguage, normalized, StringComparison.OrdinalIgnoreCase)) return;
        _currentLanguage = normalized;
        ReloadAll();
        try { LanguageChanged?.Invoke(); }
        catch (Exception ex) { Log()?.LogWarning($"ModL10n: language change handler threw: {ex.Message}"); }
    }

    private static void EnsureLanguageHook()
    {
        lock (Sync)
        {
            if (_hookInstalled) return;
            try
            {
                _currentLanguage = NormalizeCode(LocalizationUtility.CurrentLanguage.ToString());
                LocalizationUtility.OnLanguageUpdate += OnGameLanguageUpdate;
                _hookInstalled = true;
            }
            catch (Exception ex)
            {
                Log()?.LogWarning($"ModL10n: cannot subscribe to OnLanguageUpdate yet ({ex.Message}); will retry on next Register.");
            }
        }
    }

    private static void OnGameLanguageUpdate()
    {
        try { HandleLanguageUpdate(LocalizationUtility.CurrentLanguage.ToString()); }
        catch (Exception ex) { Log()?.LogWarning($"ModL10n: OnLanguageUpdate failed: {ex.Message}"); }
    }

    private static string NormalizeCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultFallback;
        return raw.Trim().ToLowerInvariant();
    }

    private static ManualLogSource? Log() => ModulusModLoaderPlugin.Log;

    private sealed class Catalog
    {
        private readonly string _modId;
        private readonly string _folder;
        private Dictionary<string, string> _active = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _fallback = new(StringComparer.OrdinalIgnoreCase);

        internal Catalog(string modId, string folder)
        {
            _modId  = modId;
            _folder = folder;
        }

        internal bool TryGet(string key, out string text)
        {
            if (_active.TryGetValue(key, out string? a)) { text = a; return true; }
            if (_fallback.TryGetValue(key, out string? f)) { text = f; return true; }
            text = key;
            return false;
        }

        internal void Reload(string activeLang, string fallbackLang)
        {
            _active   = LoadLanguage(activeLang);
            _fallback = LoadLanguage(fallbackLang);
        }

        private Dictionary<string, string> LoadLanguage(string langCode)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(langCode) || !Directory.Exists(_folder))
                return result;

            // Accept en.json, en.JSON, en-us.json (most-specific wins; shorter form fills gaps).
            string code = langCode.ToLowerInvariant();
            string baseCode = code.Contains("-") ? code.Substring(0, code.IndexOf('-')) : code;
            foreach (string candidate in new[] { baseCode, code })
            {
                string path = Path.Combine(_folder, candidate + ".json");
                if (File.Exists(path)) Merge(result, ReadJson(path));
            }
            return result;
        }

        private Dictionary<string, string> ReadJson(string path)
        {
            try
            {
                string text = File.ReadAllText(path);
                JToken root = JToken.Parse(text);
                var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Flatten(root, prefix: string.Empty, flat);
                return flat;
            }
            catch (JsonException ex)
            {
                Log()?.LogWarning($"ModL10n[{_modId}]: invalid JSON '{path}': {ex.Message}");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log()?.LogWarning($"ModL10n[{_modId}]: cannot read '{path}': {ex.Message}");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void Flatten(JToken token, string prefix, IDictionary<string, string> sink)
        {
            switch (token)
            {
                case JObject obj:
                    foreach (JProperty prop in obj.Properties())
                    {
                        string key = string.IsNullOrEmpty(prefix) ? prop.Name : prefix + "." + prop.Name;
                        Flatten(prop.Value, key, sink);
                    }
                    break;
                case JArray arr:
                    for (int i = 0; i < arr.Count; i++)
                        Flatten(arr[i], prefix + "[" + i + "]", sink);
                    break;
                case JValue val when !string.IsNullOrEmpty(prefix):
                    sink[prefix] = val.ToString(CultureInfo.InvariantCulture);
                    break;
            }
        }

        private static void Merge(IDictionary<string, string> target, IDictionary<string, string> source)
        {
            foreach (KeyValuePair<string, string> kv in source)
                target[kv.Key] = kv.Value;
        }
    }
}
