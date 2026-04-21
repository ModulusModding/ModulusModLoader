using System.Collections;
using UnityEngine;

namespace ModulusModLoader;

internal sealed class LoaderUpdateChoice
{
    internal bool Accepted;
}

/// <summary>
/// Blocking Yes/No dialog during startup (IMGUI) so the player can opt in to downloading and installing an update.
/// </summary>
internal sealed class LoaderUpdateGuiPrompt : MonoBehaviour
{
    private string _title = "Update available";
    private string _body = "";
    private bool _done;
    private Rect _windowRect;

    /// <summary>Yields until the user picks an option; sets <paramref name="target"/> before the prompt object is destroyed.</summary>
    internal static IEnumerator Run(string title, string body, LoaderUpdateChoice target)
    {
        var go = new GameObject("ModulusModLoader_UpdatePrompt");
        DontDestroyOnLoad(go);
        var p = go.AddComponent<LoaderUpdateGuiPrompt>();
        p._title = title;
        p._body = body;
        p._windowRect = new Rect((Screen.width - 440) * 0.5f, (Screen.height - 200) * 0.5f, 440, 200);

        yield return new WaitUntil(() => p._done);
        target.Accepted = p._yes;
        UnityEngine.Object.Destroy(go);
    }

    private bool _yes;

    private void Complete(bool yes)
    {
        _yes = yes;
        _done = true;
    }

    private void OnGUI()
    {
        if (_done)
            return;

        _windowRect = GUI.ModalWindow(0x4D4C5544, _windowRect, DrawModal, _title);
    }

    private void DrawModal(int windowId)
    {
        GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        GUILayout.Label(_body, GUILayout.Height(80));
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Update and restart", GUILayout.Height(36)))
            Complete(true);
        if (GUILayout.Button("Not now", GUILayout.Height(36)))
            Complete(false);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
}
