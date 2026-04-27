using UnityEngine;

namespace ModulusModLoader;

/// <summary>
/// Bottom-of-screen IMGUI strip during splash while user mods and asset bundles load (visible progress across frames).
/// </summary>
internal static class ModLoadProgress
{
    private static GameObject? _host;
    private static GUIStyle? _titleStyle;
    private static GUIStyle? _bodyStyle;

    internal static bool Active { get; private set; }
    internal static string Title { get; private set; } = "Loading mods";
    internal static string Line1 { get; private set; } = "";
    internal static string Line2 { get; private set; } = "";

    internal static void Begin()
    {
        End();
        Active = true;
        Title = "Loading mods";
        Line1 = "";
        Line2 = "";
        _host = new GameObject("ModulusModLoader_ModLoadProgress");
        Object.DontDestroyOnLoad(_host);
        _host.AddComponent<ModLoadProgressRunner>();
    }

    internal static void End()
    {
        Active = false;
        Line1 = "";
        Line2 = "";
        if (_host != null)
        {
            Object.Destroy(_host);
            _host = null;
        }
    }

    internal static void SetTitle(string title)
    {
        if (!Active)
            return;
        Title = string.IsNullOrEmpty(title) ? "Loading mods" : title;
    }

    internal static void Set(string line1, string line2 = "")
    {
        if (!Active)
            return;
        Line1 = line1 ?? "";
        Line2 = line2 ?? "";
    }

    private static void EnsureStyles()
    {
        if (_titleStyle != null)
            return;
        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        _titleStyle.normal.textColor = Color.white;
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };
        _bodyStyle.normal.textColor = new Color(0.9f, 0.93f, 1f);
    }

    private sealed class ModLoadProgressRunner : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!Active)
                return;

            EnsureStyles();
            const float pad = 12f;
            const float h = 92f;
            var rect = new Rect(pad, Screen.height - h - pad, Screen.width - pad * 2f, h);

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = prev;

            float ix = rect.x + 10f;
            float iy = rect.y + 8f;
            float iw = rect.width - 20f;
            GUI.Label(new Rect(ix, iy, iw, 22f), Title, _titleStyle!);
            GUI.Label(new Rect(ix, iy + 26f, iw, 22f), Line1, _bodyStyle!);
            GUI.Label(new Rect(ix, iy + 50f, iw, 30f), Line2, _bodyStyle!);
        }
    }
}
