using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using BepInEx.Logging;
using UnityEngine;

namespace ModulusModLoader;

/// <summary>
/// GitHub Releases update: optional check, in-game Yes/No, then download zip and apply in-process (backup move, extract, rollback on failure), cleanup <c>.bak</c>, restart game.
/// </summary>
internal static class LoaderSelfUpdate
{
    internal static IEnumerator CoRun()
    {
        if (!LoaderConfig.CheckForLoaderUpdates.Value)
            yield break;

        ManualLogSource? log = ModulusModLoaderPlugin.Log;
        if (log == null)
            yield break;

        string? earlyDll = LoaderPaths.GetThisAssemblyDllPath();
        if (!string.IsNullOrEmpty(earlyDll))
            TryPostUpdateCleanup(new DirectoryInfo(Path.GetDirectoryName(earlyDll)!), log);

        GithubLatestReleaseInfo? release = null;
        yield return GithubReleaseClient.FetchReleaseInfo(LoaderConfig.UpdateCheckTimeoutSeconds.Value, i => release = i);

        if (release == null)
            yield break;

        if (!Versioning.IsRemoteNewer(LoaderPluginInfo.Version, release.TagName))
        {
            log.LogInfo($"ModulusModLoader is up to date ({LoaderPluginInfo.Version}).");
            yield break;
        }

        string promptBody =
            $"A newer ModulusModLoader is available ({release.TagName}).\n\n" +
            $"You are running {LoaderPluginInfo.Version}.\n\n" +
            "Download and install now? The game will restart when finished.";

        var choice = new LoaderUpdateChoice();
        yield return LoaderUpdateGuiPrompt.Run("ModulusModLoader update", promptBody, choice);

        if (!choice.Accepted)
        {
            log.LogInfo("ModulusModLoader update skipped by user.");
            yield break;
        }

        string? pluginDllPathNullable = LoaderPaths.GetThisAssemblyDllPath();
        if (string.IsNullOrEmpty(pluginDllPathNullable))
        {
            log.LogError("Self-update: could not resolve this assembly path.");
            yield break;
        }

        string dllPath = pluginDllPathNullable!;

        string? gameExeNullable = LoaderGamePaths.TryGetGameExecutablePath();
        if (string.IsNullOrEmpty(gameExeNullable))
        {
            log.LogError("Self-update: could not resolve the game executable to restart. Update manually from GitHub.");
            yield break;
        }

        string gameExe = gameExeNullable!;

        string tempZip = Path.Combine(Path.GetTempPath(), $"ModulusModLoader-update-{SanitizeTag(release.TagName)}.zip");
        bool downloaded = false;
        yield return GithubReleaseClient.DownloadToFile(
            release.ZipBrowserDownloadUrl,
            LoaderConfig.UpdateDownloadTimeoutSeconds.Value,
            tempZip,
            ok => downloaded = ok);

        if (!downloaded)
            yield break;

        try
        {
            string pluginDir = Path.GetDirectoryName(dllPath)!;
            var installDir = new DirectoryInfo(pluginDir);

            using FileStream stream = File.OpenRead(tempZip);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

            if (!ZipContainsLoaderDll(archive))
            {
                log.LogError(
                    $"Update zip did not contain {LoaderPluginInfo.GithubReleaseDllEntryPath} (expected inside {LoaderPluginInfo.GithubReleaseZipAssetName}).");
                yield break;
            }

            LoaderUpdateSequence seq = LoaderUpdateSequence.Make(
                installDir,
                archive,
                filter: IsLoaderUpdateZipEntry,
                mapPath: MapLoaderUpdateZipEntry);

            LoaderUpdateResult result = seq.Execute(log);
            if (result != LoaderUpdateResult.Success)
            {
                log.LogError($"ModulusModLoader update failed ({result}).");
                yield break;
            }

            TryPostUpdateCleanup(installDir, log);
            TryRestartGame(gameExe, log);
            log.LogWarning("ModulusModLoader updated. Restarting the game.");
            Application.Quit();
        }
        catch (Exception ex)
        {
            log.LogError($"ModulusModLoader update: {ex.Message}");
        }
        finally
        {
            TryDeleteQuiet(tempZip);
        }
    }

    private static void TryPostUpdateCleanup(DirectoryInfo installDir, ManualLogSource log)
    {
        try
        {
            foreach (FileInfo file in installDir.EnumerateFiles("*.dll.bak"))
            {
                string original = file.FullName[..^4];
                if (!File.Exists(original))
                    continue;
                log.LogDebug($"Removing update backup file {file.FullName}");
                file.Delete();
            }
        }
        catch (Exception ex)
        {
            log.LogWarning($"Post-update cleanup: {ex.Message}");
        }
    }

    private static void TryRestartGame(string gameExePath, ManualLogSource log)
    {
        try
        {
            global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
            {
                FileName = gameExePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(gameExePath)!,
            });
        }
        catch (Exception ex)
        {
            log.LogError($"Failed to start game process: {ex.Message}");
        }
    }

    private static bool ZipContainsLoaderDll(ZipArchive archive)
    {
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (ZipEntryMatchesPath(entry, LoaderPluginInfo.GithubReleaseDllEntryPath))
                return true;
        }

        return false;
    }

    private static bool IsLoaderUpdateZipEntry(ZipArchiveEntry entry) =>
        ZipEntryMatchesPath(entry, LoaderPluginInfo.GithubReleaseDllEntryPath)
        || ZipEntryMatchesPath(entry, LoaderPluginInfo.GithubReleaseNewtonsoftEntryPath);

    private static bool ZipEntryMatchesPath(ZipArchiveEntry entry, string expectedZipPath)
    {
        if (string.IsNullOrEmpty(entry.Name))
            return false;

        string? normalized = NormalizeZipEntryPath(entry.FullName);
        if (normalized == null)
            return false;

        string expected = expectedZipPath.Replace('\\', '/');
        return normalized.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string MapLoaderUpdateZipEntry(ZipArchiveEntry entry)
    {
        string main = LoaderPluginInfo.GithubReleaseDllEntryPath.Replace('\\', '/');
        string newton = LoaderPluginInfo.GithubReleaseNewtonsoftEntryPath.Replace('\\', '/');
        string? normalized = NormalizeZipEntryPath(entry.FullName);
        if (normalized == null)
            throw new InvalidOperationException($"Invalid zip entry path: {entry.FullName}");
        if (normalized.Equals(main, StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(main);
        if (normalized.Equals(newton, StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(newton);
        throw new InvalidOperationException($"Unexpected zip entry: {entry.FullName}");
    }

    private static void TryDeleteQuiet(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static string SanitizeTag(string tag)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            tag = tag.Replace(c, '_');
        return tag.Length > 48 ? tag.Substring(0, 48) : tag;
    }

    private static string? NormalizeZipEntryPath(string fullName)
    {
        string s = fullName.Replace('\\', '/').Trim();
        while (s.StartsWith("./", StringComparison.Ordinal))
            s = s.Substring(2);
        if (s.StartsWith("/", StringComparison.Ordinal))
            return null;
        if (s.Contains("..", StringComparison.Ordinal))
            return null;
        return s;
    }
}

internal static class LoaderPaths
{
    internal static string? GetThisAssemblyDllPath()
    {
        string? loc = typeof(ModulusModLoaderPlugin).Assembly.Location;
        return string.IsNullOrEmpty(loc) ? null : loc;
    }
}

internal static class LoaderGamePaths
{
    internal static string? TryGetGameExecutablePath()
    {
        try
        {
            using var proc = global::System.Diagnostics.Process.GetCurrentProcess();
            string? main = proc.MainModule?.FileName;
            if (!string.IsNullOrEmpty(main) && File.Exists(main))
                return main;
        }
        catch
        {
            // ignore
        }

        string? dataDir = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(dataDir))
            return null;

        string byProduct = Path.Combine(dataDir, $"{Application.productName}.exe");
        if (File.Exists(byProduct))
            return byProduct;

        foreach (string name in new[] { "Modulus.exe", $"{Application.productName}.exe" })
        {
            string candidate = Path.Combine(dataDir, name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}

internal static class Versioning
{
    internal static bool IsRemoteNewer(string currentVersion, string remoteTag)
    {
        string remote = remoteTag.Trim().TrimStart('v', 'V');
        string current = currentVersion.Trim().TrimStart('v', 'V');

        if (!Version.TryParse(NormalizeVersionString(remote), out Version? remoteVer))
            return false;
        if (!Version.TryParse(NormalizeVersionString(current), out Version? curVer))
            return true;

        return remoteVer > curVer;
    }

    private static string NormalizeVersionString(string v)
    {
        int dash = v.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
            v = v.Substring(0, dash);
        return v;
    }
}
