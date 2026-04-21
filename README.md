# ModulusModLoader

A BepInEx-based mod loader for **Modulus**. It scans `Documents\My Games\Modulus\mods\` for user-created mods, resolves their dependencies, and loads them as BepInEx plugins.

## Requirements

- **BepInEx 5.x** installed in the Modulus game directory (extract entire BepInEx zip to the game install folder)
- **.NET Framework 4.8** targeting (built into BepInEx/Mono runtime)

## Installation

1. Build the loader (see **Building from Source**) or grab a release zip.
2. Copy `ModulusModLoader.dll` into:
   ```
   <Modulus install>\BepInEx\plugins\ModulusModLoader\
   ```
3. Launch the game. The loader creates `Documents\My Games\Modulus\mods\` on first run.

## Startup

- **Skip splash screens** (default **off**): the opening splash sequence runs as in the base game. Set **`SkipSplashScreens = true`** in **`BepInEx\config\com.zedle.modulus.modloader.cfg`** (section `[Startup]`) to jump straight to the main menu after mods load.

## Self-update (GitHub Releases)

When **`[Updates] CheckForLoaderUpdates`** is **true** (default), the loader queries **`https://github.com/ModulusModding/ModulusModLoader/releases/latest`**. If the release `tag_name` is newer than the running build, a **Yes / Not now** dialog appears during startup. **Update and restart** downloads the release zip and applies it **in process**: the installed **`ModulusModLoader.dll`** is moved aside to **`.bak`**, the new DLL is extracted from the zip, backups are cleaned up, then the game executable is started again and this process exits. **Not now** skips the download. If the swap fails, changes roll back. On failure to resolve the game exe, check the log and update manually.

Optional timeouts: **`UpdateCheckTimeoutSeconds`**, **`UpdateDownloadTimeoutSeconds`**.

**Release layout (maintainers):** publish a release asset named **`ModulusModLoader.zip`** (see `LoaderPluginInfo.GithubReleaseZipAssetName`). The zip must contain **`ModulusModLoader/ModulusModLoader.dll`** so manual installs can extract straight into **`BepInEx/plugins/`**. The updater extracts that entry for the swap script.

## Main menu

- **Status strip** (bottom-left): loader version and how many plugin mods were loaded (no paths or mod names). Scales with screen height.
- **Mods** button: opens a panel listing every mod folder (with `About/About.xml`). Toggle mods **on** or **off**; changes are saved to `Documents\My Games\Modulus\mod_registry.json` and apply on the **next** game launch (restart required).

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

```csharp
using BepInEx;
using ModulusModLoader;

[BepInPlugin("com.yourname.modulus.yourmod", "Your Mod", "0.1.0")]
[BepInDependency(LoaderPluginInfo.Guid)]
public class YourModPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo("Hello from YourMod!");

        // Subscribe to lifecycle events
        ModGameLifecycle.FactoryLoaded += () => Logger.LogInfo("Factory loaded!");
    }
}
```

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
  ├── ModulusModLoaderPlugin.cs   (entry point)
  ├── ModsRootPluginLoader.cs     (second-stage mod scanner/loader)
  ├── ModGameLifecycle.cs         (public lifecycle events for mods)
  ├── ModAssetBundles.cs            (public API + bundle load from mod folders)
  ├── ModLoadProgress.cs            (splash IMGUI strip while mods load)
  ├── LoaderSelfUpdate.cs           (GitHub download + zip apply + restart)
  ├── LoaderUpdateSequence.cs       (in-process zip: backup, extract, rollback)
  ├── LoaderUpdateGuiPrompt.cs      (startup Yes/No IMGUI for update)
  ├── GithubReleaseClient.cs        (GitHub API + zip download)
  ├── Metadata/                   (About.xml parsing)
  └── Patches/                    (Harmony patches on game classes)
ModulusModExample/                ← optional Git submodule; build its .csproj separately
```

The example template is its **own** repository (**https://github.com/ModulusModding/ModulusModExample**); `.gitmodules` points there for maintainers who want the submodule. You can delete or ignore that folder if you only care about the loader.

## License

MIT
