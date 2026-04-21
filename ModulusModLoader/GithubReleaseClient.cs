using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using UnityEngine;

namespace ModulusModLoader;

internal static class GithubReleaseClient
{
    internal static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{LoaderPluginInfo.GithubUpdateOwner}/{LoaderPluginInfo.GithubUpdateRepo}/releases/latest";

    internal static IEnumerator FetchReleaseInfo(int timeoutSeconds, Action<GithubLatestReleaseInfo?> onComplete)
    {
        using var req = UnityWebRequest.Get(LatestReleaseApiUrl);
        req.timeout = timeoutSeconds;
        req.SetRequestHeader("User-Agent", $"{LoaderPluginInfo.Name}/{LoaderPluginInfo.Version}");
        req.SetRequestHeader("Accept", "application/vnd.github+json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            ModulusModLoaderPlugin.Log?.LogWarning(
                $"GitHub release check failed: {req.result} {req.error}");
            onComplete(null);
            yield break;
        }

        try
        {
            GithubLatestReleaseInfo? info = ParseLatestRelease(req.downloadHandler.text);
            onComplete(info);
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogWarning($"GitHub release JSON parse failed: {ex.Message}");
            onComplete(null);
        }
    }

    internal static IEnumerator DownloadToFile(string browserDownloadUrl, int timeoutSeconds, string destPath, Action<bool> onComplete)
    {
        using var req = UnityWebRequest.Get(browserDownloadUrl);
        req.timeout = timeoutSeconds;
        req.SetRequestHeader("User-Agent", $"{LoaderPluginInfo.Name}/{LoaderPluginInfo.Version}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            ModulusModLoaderPlugin.Log?.LogError($"Download failed: {req.result} {req.error}");
            onComplete(false);
            yield break;
        }

        try
        {
            System.IO.File.WriteAllBytes(destPath, req.downloadHandler.data);
            onComplete(true);
        }
        catch (Exception ex)
        {
            ModulusModLoaderPlugin.Log?.LogError($"Failed writing update file: {ex.Message}");
            onComplete(false);
        }
    }

    private static GithubLatestReleaseInfo? ParseLatestRelease(string json)
    {
        JObject root = JObject.Parse(json);
        string? tagName = root["tag_name"]?.Value<string>();
        if (string.IsNullOrEmpty(tagName))
            return null;

        if (root["assets"] is not JArray assets)
            return null;

        string expected = LoaderPluginInfo.GithubReleaseZipAssetName;
        foreach (JToken item in assets)
        {
            if (item is not JObject asset)
                continue;
            string? name = asset["name"]?.Value<string>();
            if (!string.Equals(name, expected, StringComparison.OrdinalIgnoreCase))
                continue;
            string? zipUrl = asset["browser_download_url"]?.Value<string>();
            if (!string.IsNullOrEmpty(zipUrl))
                return new GithubLatestReleaseInfo(tagName!, zipUrl!);
        }

        return null;
    }
}

internal sealed class GithubLatestReleaseInfo
{
    internal GithubLatestReleaseInfo(string tagName, string zipBrowserDownloadUrl)
    {
        TagName = tagName;
        ZipBrowserDownloadUrl = zipBrowserDownloadUrl;
    }

    internal string TagName { get; }
    internal string ZipBrowserDownloadUrl { get; }
}
