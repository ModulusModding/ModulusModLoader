using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
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
            var info = ParseLatestRelease(req.downloadHandler.text);
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
        var ser = new JavaScriptSerializer();
        var root = ser.DeserializeObject(json) as Dictionary<string, object>;
        if (root == null)
            return null;

        if (!root.TryGetValue("tag_name", out object? tagObj) || tagObj is not string tagName)
            return null;

        if (!root.TryGetValue("assets", out object? assetsObj) || assetsObj is not ArrayList assets)
            return null;

        string? zipUrl = null;
        foreach (object? item in assets)
        {
            if (item is not Dictionary<string, object> asset)
                continue;
            if (!asset.TryGetValue("name", out object? nameObj) || nameObj is not string name)
                continue;
            if (!string.Equals(name, LoaderPluginInfo.GithubReleaseZipAssetName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (asset.TryGetValue("browser_download_url", out object? urlObj) && urlObj is string url)
            {
                zipUrl = url;
                break;
            }
        }

        if (string.IsNullOrEmpty(zipUrl))
            return null;

        return new GithubLatestReleaseInfo(tagName, zipUrl!);
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
