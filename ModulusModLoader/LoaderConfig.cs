using BepInEx.Configuration;

namespace ModulusModLoader;

internal static class LoaderConfig
{
    private const string LoggingSection = "Logging";
    private const string StartupSection = "Startup";
    private const string UpdatesSection = "Updates";

    internal static ConfigEntry<bool> VerboseLog { get; private set; } = null!;
    internal static ConfigEntry<bool> SkipSplashScreens { get; private set; } = null!;
    internal static ConfigEntry<bool> CheckForLoaderUpdates { get; private set; } = null!;
    internal static ConfigEntry<int> UpdateCheckTimeoutSeconds { get; private set; } = null!;
    internal static ConfigEntry<int> UpdateDownloadTimeoutSeconds { get; private set; } = null!;

    internal static void Bind(ConfigFile config)
    {
        VerboseLog = config.Bind(
            LoggingSection,
            nameof(VerboseLog),
            false,
            "Extra debug log lines: mod folder listing, load order, fingerprints, and per-plugin load details.");

        SkipSplashScreens = config.Bind(
            StartupSection,
            nameof(SkipSplashScreens),
            false,
            "If true, skips the opening splash sequence and loads the main menu scene immediately.");

        CheckForLoaderUpdates = config.Bind(
            UpdatesSection,
            nameof(CheckForLoaderUpdates),
            true,
            "If true, checks GitHub on startup when a newer ModulusModLoader exists; you are prompted in-game to download, install, and restart.");

        UpdateCheckTimeoutSeconds = config.Bind(
            UpdatesSection,
            nameof(UpdateCheckTimeoutSeconds),
            30,
            "Timeout for the GitHub API request (seconds).");

        UpdateDownloadTimeoutSeconds = config.Bind(
            UpdatesSection,
            nameof(UpdateDownloadTimeoutSeconds),
            120,
            "Timeout for downloading the release zip (seconds).");
    }
}
