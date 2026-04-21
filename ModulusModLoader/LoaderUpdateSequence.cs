using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using BepInEx.Logging;

namespace ModulusModLoader;

/// <summary>
/// In-process release zip apply: backup-replace-extract with rollback on failure (zip update pattern used by several BepInEx plugin updaters).
/// </summary>
internal enum LoaderUpdateResult
{
    None,
    Success,
    Rollback,
    FailedRollback,
}

internal sealed class LoaderUpdateSequence
{
    internal static LoaderUpdateSequence Make(
        DirectoryInfo installDir,
        ZipArchive archive,
        Func<ZipArchiveEntry, bool>? filter = null,
        Func<ZipArchiveEntry, string>? mapPath = null)
    {
        var seq = new LoaderUpdateSequence();
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (filter != null && !filter(entry))
                continue;

            string path = Path.Combine(installDir.FullName, mapPath?.Invoke(entry) ?? entry.FullName);
            bool exists = File.Exists(path);
            LoaderUpdateAction action = exists
                ? new ReplaceFileFromZipAction(path, entry)
                : new NewFileFromZipAction(path, entry);
            seq.Actions.Add(action);
        }

        return seq;
    }

    internal readonly List<LoaderUpdateAction> Actions = [];

    internal LoaderUpdateResult Execute(ManualLogSource? log)
    {
        try
        {
            foreach (LoaderUpdateAction action in Actions)
                action.PerformUpdate(log);
        }
        catch (Exception ex)
        {
            log?.LogError($"Loader update failed: {ex}");
            return Rollback(log);
        }

        try
        {
            foreach (LoaderUpdateAction action in Actions)
                action.FinishUpdate(log);
        }
        catch (Exception ex)
        {
            log?.LogWarning($"Loader update finish step: {ex.Message}");
        }

        return LoaderUpdateResult.Success;
    }

    private LoaderUpdateResult Rollback(ManualLogSource? log)
    {
        try
        {
            for (int i = Actions.Count; --i >= 0;)
                Actions[i].RevertUpdate(log);
            return LoaderUpdateResult.Rollback;
        }
        catch (Exception ex)
        {
            log?.LogError($"Loader update rollback failed: {ex}");
            return LoaderUpdateResult.FailedRollback;
        }
    }
}

internal abstract class LoaderUpdateAction
{
    internal abstract void PerformUpdate(ManualLogSource? log);
    internal abstract void FinishUpdate(ManualLogSource? log);
    internal abstract void RevertUpdate(ManualLogSource? log);
}

internal sealed class NewFileFromZipAction : LoaderUpdateAction
{
    private readonly string _path;
    private readonly ZipArchiveEntry _entry;

    internal NewFileFromZipAction(string path, ZipArchiveEntry entry)
    {
        _path = path;
        _entry = entry;
    }

    internal override void PerformUpdate(ManualLogSource? log)
    {
        log?.LogDebug($"Extracting new file to {_path}");
        _entry.ExtractToFile(_path);
    }

    internal override void FinishUpdate(ManualLogSource? log)
    {
    }

    internal override void RevertUpdate(ManualLogSource? log)
    {
        if (File.Exists(_path))
        {
            log?.LogDebug($"Removing new file {_path}");
            File.Delete(_path);
        }
    }
}

internal sealed class ReplaceFileFromZipAction : LoaderUpdateAction
{
    private readonly string _path;
    private readonly ZipArchiveEntry _entry;

    internal ReplaceFileFromZipAction(string path, ZipArchiveEntry entry)
    {
        _path = path;
        _entry = entry;
    }

    private string BackupPath => $"{_path}.bak";

    internal override void PerformUpdate(ManualLogSource? log)
    {
        log?.LogDebug($"Replacing file {_path}");

        if (File.Exists(BackupPath))
        {
            log?.LogDebug($"Removing old backup {BackupPath}");
            File.Delete(BackupPath);
        }

        log?.LogDebug($"Backing up existing file to {BackupPath}");
        File.Move(_path, BackupPath);

        log?.LogDebug($"Extracting new file to {_path}");
        _entry.ExtractToFile(_path);
    }

    internal override void FinishUpdate(ManualLogSource? log)
    {
        try
        {
            if (File.Exists(BackupPath))
                File.Delete(BackupPath);
        }
        catch
        {
            // ignore failures deleting the backup files
        }
    }

    internal override void RevertUpdate(ManualLogSource? log)
    {
        if (!File.Exists(BackupPath))
            return;

        if (File.Exists(_path))
        {
            log?.LogDebug($"Removing new file {_path}");
            File.Delete(_path);
        }

        log?.LogDebug($"Restoring backup file {BackupPath}");
        File.Move(BackupPath, _path);
    }
}
