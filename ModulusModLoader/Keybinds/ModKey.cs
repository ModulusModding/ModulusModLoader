using System;
using System.Collections.Generic;

namespace ModulusModLoader.Keybinds;

/// <summary>
/// Strongly-typed default keys/buttons for <see cref="ModKeybind"/> registrations.
/// Convert to a New Input System path string with <see cref="ModBindingPath.For(ModKey)"/> /
/// <see cref="ModBindingPath.For(ModMouseButton)"/>.
/// Gamepads are not supported by Modulus, so no gamepad enum is exposed.
/// </summary>
public enum ModKey
{
    None = 0,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Top-row digits
    Digit0, Digit1, Digit2, Digit3, Digit4,
    Digit5, Digit6, Digit7, Digit8, Digit9,

    // Function row
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Editing / control
    Escape, Tab, Backspace, Enter, Space, CapsLock,
    LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt, RightAlt,
    LeftMeta, RightMeta, ContextMenu,

    // Arrows
    UpArrow, DownArrow, LeftArrow, RightArrow,

    // Navigation block
    Insert, Delete, Home, End, PageUp, PageDown,
    PrintScreen, ScrollLock, Pause,

    // Punctuation / OEM
    BackQuote, Minus, Equals, LeftBracket, RightBracket, Backslash,
    Semicolon, Quote, Comma, Period, Slash,

    // Numpad
    Numpad0, Numpad1, Numpad2, Numpad3, Numpad4,
    Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
    NumpadEnter, NumpadAdd, NumpadSubtract, NumpadMultiply, NumpadDivide,
    NumpadDecimal, NumpadEquals, NumpadPlusMinus, NumLock,
}

/// <summary>Mouse buttons exposed to mod authors.</summary>
public enum ModMouseButton
{
    None = 0,
    Left,
    Right,
    Middle,
    Forward,
    Back,
}

/// <summary>
/// Converts the <see cref="ModKey"/> / <see cref="ModMouseButton"/> enums to
/// New Input System binding paths (e.g. <c>&lt;Keyboard&gt;/f5</c>).
/// </summary>
public static class ModBindingPath
{
    private static readonly Dictionary<ModKey, string> KeyMap = BuildKeyMap();
    private static readonly Dictionary<ModMouseButton, string> MouseMap = new()
    {
        [ModMouseButton.None]    = string.Empty,
        [ModMouseButton.Left]    = "<Mouse>/leftButton",
        [ModMouseButton.Right]   = "<Mouse>/rightButton",
        [ModMouseButton.Middle]  = "<Mouse>/middleButton",
        [ModMouseButton.Forward] = "<Mouse>/forwardButton",
        [ModMouseButton.Back]    = "<Mouse>/backButton",
    };

    /// <summary>Returns the <c>&lt;Keyboard&gt;/...</c> path for a key.</summary>
    public static string For(ModKey key) =>
        KeyMap.TryGetValue(key, out string? p) ? p : string.Empty;

    /// <summary>Returns the <c>&lt;Mouse&gt;/...</c> path for a mouse button.</summary>
    public static string For(ModMouseButton button) =>
        MouseMap.TryGetValue(button, out string? p) ? p : string.Empty;

    private static Dictionary<ModKey, string> BuildKeyMap()
    {
        var m = new Dictionary<ModKey, string>
        {
            [ModKey.None] = string.Empty,

            [ModKey.Escape]      = "<Keyboard>/escape",
            [ModKey.Tab]         = "<Keyboard>/tab",
            [ModKey.Backspace]   = "<Keyboard>/backspace",
            [ModKey.Enter]       = "<Keyboard>/enter",
            [ModKey.Space]       = "<Keyboard>/space",
            [ModKey.CapsLock]    = "<Keyboard>/capsLock",
            [ModKey.LeftShift]   = "<Keyboard>/leftShift",
            [ModKey.RightShift]  = "<Keyboard>/rightShift",
            [ModKey.LeftCtrl]    = "<Keyboard>/leftCtrl",
            [ModKey.RightCtrl]   = "<Keyboard>/rightCtrl",
            [ModKey.LeftAlt]     = "<Keyboard>/leftAlt",
            [ModKey.RightAlt]    = "<Keyboard>/rightAlt",
            [ModKey.LeftMeta]    = "<Keyboard>/leftMeta",
            [ModKey.RightMeta]   = "<Keyboard>/rightMeta",
            [ModKey.ContextMenu] = "<Keyboard>/contextMenu",

            [ModKey.UpArrow]     = "<Keyboard>/upArrow",
            [ModKey.DownArrow]   = "<Keyboard>/downArrow",
            [ModKey.LeftArrow]   = "<Keyboard>/leftArrow",
            [ModKey.RightArrow]  = "<Keyboard>/rightArrow",

            [ModKey.Insert]      = "<Keyboard>/insert",
            [ModKey.Delete]      = "<Keyboard>/delete",
            [ModKey.Home]        = "<Keyboard>/home",
            [ModKey.End]         = "<Keyboard>/end",
            [ModKey.PageUp]      = "<Keyboard>/pageUp",
            [ModKey.PageDown]    = "<Keyboard>/pageDown",
            [ModKey.PrintScreen] = "<Keyboard>/printScreen",
            [ModKey.ScrollLock]  = "<Keyboard>/scrollLock",
            [ModKey.Pause]       = "<Keyboard>/pause",

            [ModKey.BackQuote]    = "<Keyboard>/backquote",
            [ModKey.Minus]        = "<Keyboard>/minus",
            [ModKey.Equals]       = "<Keyboard>/equals",
            [ModKey.LeftBracket]  = "<Keyboard>/leftBracket",
            [ModKey.RightBracket] = "<Keyboard>/rightBracket",
            [ModKey.Backslash]    = "<Keyboard>/backslash",
            [ModKey.Semicolon]    = "<Keyboard>/semicolon",
            [ModKey.Quote]        = "<Keyboard>/quote",
            [ModKey.Comma]        = "<Keyboard>/comma",
            [ModKey.Period]       = "<Keyboard>/period",
            [ModKey.Slash]        = "<Keyboard>/slash",

            [ModKey.NumLock]         = "<Keyboard>/numLock",
            [ModKey.NumpadEnter]     = "<Keyboard>/numpadEnter",
            [ModKey.NumpadAdd]       = "<Keyboard>/numpadPlus",
            [ModKey.NumpadSubtract]  = "<Keyboard>/numpadMinus",
            [ModKey.NumpadMultiply]  = "<Keyboard>/numpadMultiply",
            [ModKey.NumpadDivide]    = "<Keyboard>/numpadDivide",
            [ModKey.NumpadDecimal]   = "<Keyboard>/numpadPeriod",
            [ModKey.NumpadEquals]    = "<Keyboard>/numpadEquals",
            [ModKey.NumpadPlusMinus] = "<Keyboard>/numpadPlusMinus",
        };

        for (char c = 'A'; c <= 'Z'; c++)
        {
            ModKey k = (ModKey)Enum.Parse(typeof(ModKey), c.ToString());
            m[k] = "<Keyboard>/" + char.ToLowerInvariant(c);
        }
        for (int d = 0; d <= 9; d++)
        {
            ModKey k = (ModKey)Enum.Parse(typeof(ModKey), "Digit" + d);
            m[k] = "<Keyboard>/" + d;
        }
        for (int f = 1; f <= 12; f++)
        {
            ModKey k = (ModKey)Enum.Parse(typeof(ModKey), "F" + f);
            m[k] = "<Keyboard>/f" + f;
        }
        for (int n = 0; n <= 9; n++)
        {
            ModKey k = (ModKey)Enum.Parse(typeof(ModKey), "Numpad" + n);
            m[k] = "<Keyboard>/numpad" + n;
        }

        return m;
    }
}
