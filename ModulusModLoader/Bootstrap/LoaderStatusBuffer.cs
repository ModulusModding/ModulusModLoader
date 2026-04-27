using System.Collections.Generic;

namespace ModulusModLoader;

/// <summary>Rolling status lines for logging / future use (BepInEx log mirrors the same info).</summary>
internal static class LoaderStatusBuffer
{
    private const int MaxLines = 20;
    private static readonly object Gate = new();
    private static readonly List<string> Lines = new();

    internal static void Add(string line)
    {
        lock (Gate)
        {
            Lines.Add(line);
            while (Lines.Count > MaxLines)
                Lines.RemoveAt(0);
        }
    }

    internal static void Clear()
    {
        lock (Gate)
            Lines.Clear();
    }

    internal static IReadOnlyList<string> Snapshot()
    {
        lock (Gate)
            return Lines.ToArray();
    }
}
