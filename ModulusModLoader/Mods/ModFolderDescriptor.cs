using ModulusModLoader.Metadata;

namespace ModulusModLoader;

internal sealed class ModFolderDescriptor
{
    internal ModFolderDescriptor(string rootPath, string folderName, ModAbout about, bool aboutInvalid)
    {
        RootPath = rootPath;
        FolderName = folderName;
        About = about;
        AboutInvalid = aboutInvalid;
    }

    internal string RootPath { get; }
    internal string FolderName { get; }
    internal ModAbout About { get; }
    internal bool AboutInvalid { get; }

    internal string EffectiveModId =>
        !string.IsNullOrWhiteSpace(About.ModID) ? About.ModID! : FolderName;

    internal string EffectiveName =>
        !string.IsNullOrWhiteSpace(About.Name) ? About.Name! : FolderName;
}
