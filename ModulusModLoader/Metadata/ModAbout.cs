using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ModulusModLoader.Metadata;

[XmlRoot("ModMetadata")]
public class ModAbout
{
    [XmlElement("Name")]
    public string? Name;

    [XmlElement("ModID")]
    public string? ModID;

    [XmlElement("Author")]
    public string? Author;

    [XmlElement("Version")]
    public string? Version;

    [XmlElement("Description")]
    public string? Description;

    [XmlIgnore]
    public string? InGameDescription;

    [XmlElement("InGameDescription", IsNullable = true)]
    public CDataString? InGameDescriptionCData
    {
        get => string.IsNullOrEmpty(InGameDescription) ? null : new CDataString { Value = InGameDescription };
        set => InGameDescription = value?.Value;
    }

    [XmlElement("ChangeLog", IsNullable = true)]
    public string? ChangeLog;

    [XmlArray("Tags"), XmlArrayItem("Tag")]
    public List<string>? Tags;

    [XmlArray("DependsOn"), XmlArrayItem("Mod")]
    public List<ModReference>? DependsOn;

    [XmlArray("OrderBefore"), XmlArrayItem("Mod")]
    public List<ModReference>? OrderBefore;

    [XmlArray("OrderAfter"), XmlArrayItem("Mod")]
    public List<ModReference>? OrderAfter;
}

public class ModReference
{
    [XmlAttribute("ModID")]
    public string? ModID;

    [XmlAttribute("Version")]
    public string? Version;

    public override string ToString()
    {
        if (!IsValid) return "Invalid";
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(ModID))
            sb.AppendFormat("ModID: {0}", ModID);
        if (!string.IsNullOrEmpty(Version))
            sb.AppendFormat("{0}Version: {1}", sb.Length == 0 ? "" : ", ", Version);
        return sb.ToString();
    }

    public bool IsValid => !string.IsNullOrEmpty(ModID);
}

public class CDataString : IXmlSerializable
{
    public string? Value;

    public XmlSchema? GetSchema() => null;

    public void ReadXml(XmlReader reader) => Value = reader.ReadElementContentAsString();

    public void WriteXml(XmlWriter writer)
    {
        if (Value != null)
            writer.WriteCData(Value);
    }
}
