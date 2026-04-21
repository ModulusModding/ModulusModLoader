namespace ModulusModLoader;

/// <summary>
/// Stable identity for <see cref="BepInEx.BepInDependency"/> from gameplay mods.
/// Use <see cref="Guid"/> in <c>[BepInDependency(LoaderPluginInfo.Guid)]</c>.
/// </summary>
public static class LoaderPluginInfo
{
    public const string Guid = "com.zedle.modulus.modloader";
    public const string Name = "ModulusModLoader";
    public const string Version = "0.1.0";

    /// <summary>GitHub org for self-update checks (<c>releases/latest</c>).</summary>
    public const string GithubUpdateOwner = "ModulusModding";

    /// <summary>GitHub repo name for self-update checks.</summary>
    public const string GithubUpdateRepo = "ModulusModLoader";

    /// <summary>
    /// Release zip asset name. The archive must contain <c>ModulusModLoader/ModulusModLoader.dll</c>
    /// so it can be extracted straight into <c>BepInEx/plugins/</c> with the correct plugin folder layout.
    /// </summary>
    public const string GithubReleaseZipAssetName = "ModulusModLoader.zip";

    /// <summary>Relative path inside <see cref="GithubReleaseZipAssetName"/> to the loader DLL.</summary>
    public const string GithubReleaseDllEntryPath = "ModulusModLoader/ModulusModLoader.dll";
}
