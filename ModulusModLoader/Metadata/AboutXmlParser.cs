using System;
using System.IO;
using System.Xml.Serialization;
using BepInEx.Logging;

namespace ModulusModLoader.Metadata;

internal static class AboutXmlParser
{
    private static readonly XmlSerializer Serializer = new(typeof(ModAbout));

    internal static bool TryLoad(string aboutPath, ManualLogSource log, out ModAbout about, out string? error)
    {
        about = new ModAbout();
        error = null;
        try
        {
            using var stream = File.OpenRead(aboutPath);
            if (Serializer.Deserialize(stream) is ModAbout parsed)
            {
                about = parsed;
                return true;
            }

            error = "Root element was not ModMetadata.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            log.LogError($"[Invalid About.xml] {aboutPath}: {ex.Message}");
            return false;
        }
    }
}
