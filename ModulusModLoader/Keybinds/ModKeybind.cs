using System;
using System.Collections.Generic;
using ModulusModLoader.Keybinds;
using UnityEngine.InputSystem;

namespace ModulusModLoader;

/// <summary>
/// Register keybinds that appear in the game's Settings → Controls screen and persist with the
/// same binding-override JSON as vanilla actions.
/// </summary>
public static class ModKeybind
{
    /// <summary>
    /// Registers a keybind under the default category <c>Mods</c>.
    /// Prefer <see cref="Register(string,string,string,string,string)"/> so your section matches the game header style.
    /// </summary>
    public static InputAction? Register(
        string modId,
        string actionId,
        string displayName,
        string defaultBindingPath) =>
        Register(modId, actionId, displayName, defaultBindingPath, category: "Mods");

    /// <summary>
    /// Registers a keybind. Safe to call from plugin <c>Awake</c> / <c>OnEnable</c>.
    /// The same <paramref name="modId"/> + <paramref name="actionId"/> replaces a previous registration.
    /// </summary>
    /// <param name="category">
    /// Section header in Settings → Controls (same string for multiple bindings groups them, like vanilla categories).
    /// </param>
    /// <param name="modId">Stable mod id (e.g. BepInEx plugin GUID or About.xml ModID).</param>
    /// <param name="actionId">Unique id within the mod (letters, digits, underscore).</param>
    /// <param name="displayName">Shown as the row label (plain text).</param>
    /// <param name="defaultBindingPath">New Input System path, e.g. <c>&lt;Keyboard&gt;/f5</c>.</param>
    /// <returns>
    /// The live <see cref="InputAction"/> when the game's settings asset is available; otherwise <c>null</c>
    /// (call again from <c>Start</c> or after <see cref="ModGameLifecycle.GameStarted"/>).
    /// </returns>
    public static InputAction? Register(
        string modId,
        string actionId,
        string displayName,
        string defaultBindingPath,
        string category)
    {
        if (string.IsNullOrWhiteSpace(modId)) throw new ArgumentException("modId is required.", nameof(modId));
        if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("actionId is required.", nameof(actionId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("displayName is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(defaultBindingPath))
            throw new ArgumentException("defaultBindingPath is required.", nameof(defaultBindingPath));
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("category is required.", nameof(category));

        return ModKeybindIntegration.Register(
            modId.Trim(), actionId.Trim(), displayName.Trim(), defaultBindingPath.Trim(), category.Trim());
    }

    /// <summary>
    /// Registers a keybind whose default is a <see cref="ModKey"/> (no raw <c>&lt;Keyboard&gt;/...</c> string).
    /// </summary>
    public static InputAction? Register(
        string modId,
        string actionId,
        string displayName,
        ModKey defaultKey,
        string category) =>
        Register(modId, actionId, displayName, ModBindingPath.For(defaultKey), category);

    /// <summary>
    /// Registers a keybind whose default is a <see cref="ModMouseButton"/>.
    /// </summary>
    public static InputAction? Register(
        string modId,
        string actionId,
        string displayName,
        ModMouseButton defaultButton,
        string category) =>
        Register(modId, actionId, displayName, ModBindingPath.For(defaultButton), category);

    /// <summary>Try resolve a registered action (after <see cref="Register"/>).</summary>
    public static bool TryGetAction(string modId, string actionId, out InputAction? action)
    {
        action = null;
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(actionId)) return false;
        return ModKeybindIntegration.TryGetAction(modId.Trim(), actionId.Trim(), out action);
    }

    /// <summary>All registrations (read-only snapshot).</summary>
    public static IReadOnlyList<ModKeybindRegistration> Registrations => ModKeybindIntegration.RegistrationsSnapshot;

    /// <summary>
    /// Called after late-loaded mods register keybinds. Rebuilds runtime rebind metadata and modifier listeners.
    /// The Controls UI is refreshed the next time the settings panel is opened.
    /// </summary>
    public static void NotifyRegistrationsChanged() => ModKeybindIntegration.NotifyRegistrationsChanged();
}

/// <summary>Metadata for one mod-defined keybind.</summary>
public sealed class ModKeybindRegistration
{
    /// <summary>For loader use only; mods receive instances from <see cref="ModKeybind.Registrations"/>.</summary>
    internal ModKeybindRegistration(
        string modId,
        string actionId,
        string displayName,
        string defaultBindingPath,
        Guid bindingGuid,
        string category)
    {
        ModId              = modId;
        ActionId           = actionId;
        DisplayName        = displayName;
        DefaultBindingPath = defaultBindingPath;
        BindingGuid        = bindingGuid;
        Category           = category;
    }

    public string ModId { get; }
    public string ActionId { get; }
    public string DisplayName { get; }
    public string DefaultBindingPath { get; }
    public Guid BindingGuid { get; }
    /// <summary>Controls section header; same value across bindings merges them under one subtitle.</summary>
    public string Category { get; }
}
