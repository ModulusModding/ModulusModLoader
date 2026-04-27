namespace ModulusModLoader.Config;

/// <summary>
/// Stable <see cref="Localization.ModL10n"/> keys for the loader-built BepInEx settings UI.
/// All strings live under the <see cref="RootPrefix"/> object in JSON (e.g. <c>bepinex-config.entries.general.verbose.label</c>).
/// Section and definition keys are normalized (lowercase, spaces to underscores).
/// </summary>
public static class ModConfigL10nKeys
{
    /// <summary>Root object name in each language JSON (nested under this key).</summary>
    public const string RootPrefix = "bepinex-config";

    /// <summary>Description body: <c>bepinex-config.entries.{section}.{key}.description</c>.</summary>
    public static string EntryDescription(string? section, string key) =>
        $"{RootPrefix}.entries.{Segment(section)}.{Segment(key)}.description";

    /// <summary>Row title when present in JSON; otherwise the BepInEx key is shown.</summary>
    public static string EntryLabel(string? section, string key) =>
        $"{RootPrefix}.entries.{Segment(section)}.{Segment(key)}.label";

    /// <summary>One dropdown row for an enum member (raw enum name as in <c>Enum.GetName</c>).</summary>
    public static string EnumMember(string? section, string key, string enumMemberName) =>
        $"{RootPrefix}.enums.{Segment(section)}.{Segment(key)}.{Segment(enumMemberName)}";

    /// <summary>Section subtitle: <c>bepinex-config.sections.{section}</c>.</summary>
    public static string SectionHeader(string? section) =>
        $"{RootPrefix}.sections.{Segment(section)}";

    /// <summary>Normalizes a BepInEx section or key for lookup (lowercase, trim, spaces to underscores).</summary>
    public static string Segment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "general";
        return value!.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
