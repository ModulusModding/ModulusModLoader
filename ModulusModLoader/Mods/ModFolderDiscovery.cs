using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using ModulusModLoader.Metadata;

namespace ModulusModLoader;

internal static class ModFolderDiscovery
{
    internal static List<ModFolderDescriptor> Discover(string modsRoot, ManualLogSource log)
    {
        var list = new List<ModFolderDescriptor>();
        if (!Directory.Exists(modsRoot))
            return list;

        foreach (string dir in Directory.GetDirectories(modsRoot))
        {
            string aboutPath = Path.Combine(dir, "About", "About.xml");
            if (!File.Exists(aboutPath))
                continue;

            string folderName = Path.GetFileName(dir);
            if (TryLoadDescriptor(dir, folderName, aboutPath, log, out var descriptor))
                list.Add(descriptor);
        }

        return list;
    }

    private static bool TryLoadDescriptor(
        string rootPath,
        string folderName,
        string aboutPath,
        ManualLogSource log,
        out ModFolderDescriptor descriptor)
    {
        if (!AboutXmlParser.TryLoad(aboutPath, log, out var about, out _))
        {
            about.ModID ??= folderName;
            about.Name ??= folderName;
            descriptor = new ModFolderDescriptor(rootPath, folderName, about, aboutInvalid: true);
            return true;
        }

        if (string.IsNullOrWhiteSpace(about.ModID))
            about.ModID = folderName;
        if (string.IsNullOrWhiteSpace(about.Name))
            about.Name = folderName;

        descriptor = new ModFolderDescriptor(rootPath, folderName, about, aboutInvalid: false);
        return true;
    }
}
