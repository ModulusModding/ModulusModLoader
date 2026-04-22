# ModulusModLoader

A BepInEx-based mod loader for **Modulus**. It scans `Documents\My Games\Modulus\mods\` for user-created mods, resolves their dependencies, and loads them as BepInEx plugins.

## Requirements

- **BepInEx 5.x** installed in the Modulus game directory (extract entire BepInEx zip to the game install folder)
- **.NET Framework 4.8** targeting (built into BepInEx/Mono runtime)

## Installation

1. Build the loader (see **Building from Source**) or grab a release zip.
2. Copy **`ModulusModLoader.dll`** and **`Newtonsoft.Json.dll`** into:
   ```
   <Modulus install>\BepInEx\plugins\ModulusModLoader\
   ```
   (Official release zips include both files under the `ModulusModLoader\` folder; extract so both DLLs sit in that plugin folder.)
3. Launch the game. The loader creates `Documents\My Games\Modulus\mods\` on first run.

## Startup

- **Skip splash screens** (default **off**): the opening splash sequence runs as in the base game. Set **`SkipSplashScreens = true`** in **`BepInEx\config\com.zedle.modulus.modloader.cfg`** (section `[Startup]`) to jump straight to the main menu after mods load.

## Self-update (GitHub Releases)

When **`[Updates] CheckForLoaderUpdates`** is **true** (default), the loader queries **`https://github.com/ModulusModding/ModulusModLoader/releases/latest`**. If the release `tag_name` is newer than the running build, a **Yes / Not now** dialog appears during startup. **Update and restart** downloads the release zip and applies it **in process**: the installed **`ModulusModLoader.dll`** is moved aside to **`.bak`**, the new DLL is extracted from the zip, backups are cleaned up, then the game executable is started again and this process exits. **Not now** skips the download. If the swap fails, changes roll back. On failure to resolve the game exe, check the log and update manually.

Optional timeouts: **`UpdateCheckTimeoutSeconds`**, **`UpdateDownloadTimeoutSeconds`**.

**Release layout (maintainers):** publish a release asset named **`ModulusModLoader.zip`** (see `LoaderPluginInfo.GithubReleaseZipAssetName`). The zip must contain **`ModulusModLoader/ModulusModLoader.dll`** and **`ModulusModLoader/Newtonsoft.Json.dll`** so manual installs and the self-updater can extract straight into **`BepInEx/plugins/ModulusModLoader/`**.

## Main menu

- **Status strip** (bottom-left): loader version and how many plugin mods were loaded (no paths or mod names). Scales with screen height.
- **Mods** button: opens a panel listing every mod folder (with `About/About.xml`). Toggle mods **on** or **off**; changes are saved to `Documents\My Games\Modulus\mod_registry.json` and apply on the **next** game launch (restart required).

## Keybinds (vanilla Settings → Controls)

Mods can register actions that appear in the **same** Controls screen as the base game. Each registration supplies a **category** string: that text is the **section header** (like vanilla **Tools** or **Inside Operators**), and every binding that uses the same category string is listed under that header. If you omit the category overload, the default header is **Mods**. Bindings are stored in the game's normal settings save (same `InputActionAsset` override JSON as vanilla).

Defaults can be expressed with strongly-typed enums (`ModKey`, `ModMouseButton`) instead of raw `<Keyboard>/...` strings. Modulus does not support gamepads, so no gamepad enum is exposed:

```csharp
using ModulusModLoader;
using ModulusModLoader.Keybinds;
using UnityEngine.InputSystem;

InputAction? myAction = ModKeybind.Register(
    pluginGuidOrModId,
    "ToggleOverlay",
    "My mod: Toggle overlay",
    ModKey.F9,                  // or ModMouseButton.Middle, etc.
    PluginInfo.PluginName);     // section header in Controls

if (myAction != null && myAction.WasPressedThisFrame()) { /* ... */ }
```

The original string overload is still supported for cases where you need a path the enums do not cover (`ModKeybind.Register(modId, actionId, name, "<Keyboard>/numpad7", category)`).

If `Register` returns `null`, the settings `InputActionAsset` is not in memory yet; call `Register` again from `Start` or after `ModGameLifecycle.GameStarted`.

Register from plugin `Awake` when possible (or as soon as your mod loads). After **second-stage** mods finish loading, the loader refreshes the rebind runtime so late registrations still work; reopen **Settings → Controls** if that tab was already open.

## Localization (`ModL10n`)

Ship one JSON file per language under `<YourMod>/Localization/`:

```
YourMod/
  Localization/
    en.json
    de.json
    fr.json
```

JSON is either flat (`"key": "value"`) or nested (collapsed with dot keys, e.g. `greeting.hello`). Register the folder once during `Awake` and look strings up by key. The catalog reloads automatically when the player switches language in vanilla settings.

```csharp
using System.IO;
using System.Reflection;
using ModulusModLoader.Localization;

string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
ModL10n.Register(PluginInfo.PluginGuid, pluginFolder);   // expects {pluginFolder}/Localization/<lang>.json

string greeting = ModL10n.Get(PluginInfo.PluginGuid, "greeting.hello", fallback: "Hello");
string formatted = ModL10n.Format(PluginInfo.PluginGuid, "log.keybindPressed", greeting, scale);
```

Missing keys fall back to (1) the configured fallback language (default `en`, change with `ModL10n.SetFallbackLanguage`), (2) the supplied `fallback` argument, (3) the literal key.

## Mod settings panel (BepInEx `ConfigEntry` editor)

Anything you bind via BepInEx's `Config.Bind(...)` automatically appears in the **Mods** menu's **SETTINGS** subsection (under the About text) when your mod is selected:

```csharp
using BepInEx.Configuration;

private ConfigEntry<bool>  _verbose;
private ConfigEntry<int>   _logEveryN;
private ConfigEntry<float> _scale;
private ConfigEntry<MyEnum> _mode;

private void Awake() {
    _verbose   = Config.Bind("General", "Verbose", true, "Log extra detail.");
    _logEveryN = Config.Bind("General", "LogEveryNFrames", 60,
                    new ConfigDescription("Log heartbeat cadence.", new AcceptableValueRange<int>(1, 600)));
    _scale     = Config.Bind("Tuning", "Scale", 1.0f,
                    new ConfigDescription("Tunable float.", new AcceptableValueRange<float>(0.1f, 5f)));
    _mode      = Config.Bind("General", "Mode", MyEnum.A, "Pick a mode.");
}
```

The loader picks the editor based on the entry type:

| Type | Editor |
|------|--------|
| `bool` | ON/OFF pill toggle |
| `enum`, `AcceptableValueList<string>` | Dropdown |
| `int`/`float` + `AcceptableValueRange<T>` | Slider with value label |
| Other numeric types | Numeric `TMP_InputField` |
| Anything else (incl. `string`) | Text `TMP_InputField` |

Edits are written back to the live `ConfigEntry` and BepInEx persists them to `BepInEx/config/<plugin>.cfg` immediately.

## Creating a Mod

See the **ModulusModExample** project for a working template.

### Mod Folder Layout

```
Documents\My Games\Modulus\mods\
  YourMod\
    About\
      About.xml          ← required; mod metadata
    YourMod.dll          ← your BepInEx plugin assembly
    Content\Things.bundle             ← optional; any subfolder under your mod
```

### Asset bundles

Put **Unity asset bundle** files anywhere under your mod folder (next to `About.xml`, or in subfolders). Typical Unity build output is **`.bundle`** or **`.assetbundle`**; the loader finds those **recursively**. It also loads **`.assets`** bundle files if you ship that layout.

They load **before** your plugin’s **`Awake`**.

### About.xml Format

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetadata>
  <Name>Your Mod Name</Name>
  <ModID>com.yourname.modulus.yourmod</ModID>
  <Author>Your Name</Author>
  <Version>0.1.0</Version>
  <Description>What your mod does.</Description>
</ModMetadata>
```

### Plugin Skeleton

If your project's `*.VS.props` imports **`Modulus.Mod.targets` next to it** (the example template does), then `PluginInfo.PluginGuid` / `PluginName` / `PluginVersion` are codegened from `About.xml` at build time and the `[BepInPlugin]` attribute can reference them directly. **You do not need to maintain a `PluginInfo.cs` file.**

```csharp
using BepInEx;
using ModulusModLoader;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
[BepInDependency(LoaderPluginInfo.Guid)]
public class YourModPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo("Hello from YourMod!");

        ModGameLifecycle.FactoryLoaded += () => Logger.LogInfo("Factory loaded!");
    }
}
```

If you choose not to use `Modulus.Mod.targets`, hand-write the same constants and pass them as raw strings.

### Lifecycle Events

`ModGameLifecycle` provides events that fire on the Unity main thread:

| Event | When it fires |
|-------|--------------|
| `GameStarted` | Once, after splash screens and mod loading completes |
| `SceneLoaded(string)` | Each time a scene finishes loading (receives scene name) |
| `FactoryLoaded` | After the factory has finished loading a save |
| `FactoryClearing` | Before the factory/level is cleared |

### Mod Ordering

`About.xml` supports ordering directives:

```xml
<DependsOn>
  <Mod ModID="com.other.mod" />
</DependsOn>
<OrderBefore>
  <Mod ModID="com.later.mod" />
</OrderBefore>
<OrderAfter>
  <Mod ModID="com.earlier.mod" />
</OrderAfter>
```

## Building from Source

1. Clone this repository (submodules are **not** required to compile the loader).
2. Copy `ModulusModLoader.VS.User.props.example` to `ModulusModLoader.VS.User.props` and set **`SteamLibraryDirectory`** to the folder that contains your `Modulus` install (usually `...\Steam\steamapps\common`).
3. From the repo root (next to `ModulusModLoader.sln`):

   ```powershell
   dotnet build ModulusModLoader.sln -c Release -p:Platform=x64
   ```

   The loader DLL is written under `ModulusModLoader\bin\x64\Release\` and the build also copies it to **`Modulus\BepInEx\plugins\ModulusModLoader\`** when that path exists.

**Optional:** `ModulusModExample` is a separate template (Git submodule under `ModulusModExample\`). To fetch it: `git submodule update --init`. Build it with `dotnet build ModulusModExample\ModulusModExample.csproj` (see that folder’s README). It is **not** part of `ModulusModLoader.sln`.

## Project Structure

```
ModulusModLoader.sln              ← builds ModulusModLoader only
ModulusModLoader/
  ├── Bootstrap/    (BepInPlugin entry, LoaderConfig, LoaderPluginInfo, status buffer, plugin-info reflection)
  ├── Mods/         (folder discovery + ordering + registry + lifecycle events + asset bundles + load progress)
  │   └── Metadata/ (About.xml parsing)
  ├── Keybinds/     (ModKeybind public API, ModKey/ModMouseButton enums, runtime injection)
  ├── Localization/ (ModL10n - per-mod JSON catalogs, language hot reload)
  ├── Config/       (ModConfigPanel - vanilla-styled BepInEx ConfigEntry editors)
  ├── Menu/         (in-game UI: HUD, main-menu button, mods overlay, list controller)
  ├── Update/       (GitHub Releases self-update)
  └── Patches/      (Harmony patches on game classes)
ModulusModExample/                ← optional Git submodule; build its .csproj separately
```

The example template is its **own** repository (**https://github.com/ModulusModding/ModulusModExample**); `.gitmodules` points there for maintainers who want the submodule. You can delete or ignore that folder if you only care about the loader.

## License

MIT
