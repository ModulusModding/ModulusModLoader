using System.Text.RegularExpressions;

namespace ModulusModLoader;

/// <summary>
/// Converts a subset of BBCode (as used in RimWorld/Modulus About.xml descriptions)
/// to TextMeshPro rich-text markup.
/// </summary>
internal static class BbCodeToTmp
{
    private static readonly (Regex Re, string Replacement)[] Rules =
    {
        // Basic formatting
        (Re(@"\[b\]"),   "<b>"),
        (Re(@"\[/b\]"),  "</b>"),
        (Re(@"\[i\]"),   "<i>"),
        (Re(@"\[/i\]"),  "</i>"),
        (Re(@"\[u\]"),   "<u>"),
        (Re(@"\[/u\]"),  "</u>"),
        (Re(@"\[s\]"),   "<s>"),
        (Re(@"\[/s\]"),  "</s>"),

        // Color: [color=#rrggbb] or [color=name]
        (Re(@"\[color=([^\]]+)\]"),  "<color=$1>"),
        (Re(@"\[/color\]"),          "</color>"),

        // Size: [size=N] — keep the number as-is; TMP treats it as px
        (Re(@"\[size=(\d+(?:\.\d+)?)\]"),  "<size=$1>"),
        (Re(@"\[/size\]"),                 "</size>"),

        // Strip hyperlinks but keep their text
        (Re(@"\[url=[^\]]*\]"),  ""),
        (Re(@"\[/url\]"),        ""),

        // Strip embedded images entirely
        (Re(@"\[img\].*?\[/img\]"),  ""),

        // Lists → newline bullet points
        (Re(@"\[list\]"),   "\n"),
        (Re(@"\[/list\]"),  "\n"),
        (Re(@"\[\*\]"),     "\n• "),

        // Strip any remaining unknown tags
        (Re(@"\[[^\]]*\]"), ""),
    };

    internal static string Convert(string? bbcode)
    {
        if (string.IsNullOrWhiteSpace(bbcode))
            return string.Empty;

        string result = bbcode!;
        foreach (var (re, rep) in Rules)
            result = re.Replace(result, rep);

        // Collapse 3+ consecutive newlines into two (paragraph break)
        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        return result.Trim();
    }

    private static Regex Re(string pattern) =>
        new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
}
