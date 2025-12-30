using System.Xml.Serialization;

namespace ClipMate.Core.Models.Export;

/// <summary>
/// XML export/import data structure using serializable DTOs.
/// </summary>
[XmlRoot("ClipMateExport")]
public class XmlExportData
{
    [XmlAttribute]
    public DateTime ExportedAt { get; set; }

    [XmlAttribute]
    public string Version { get; set; } = "1.0";

    [XmlAttribute]
    public int ClipCount { get; set; }

    [XmlAttribute]
    public int CollectionCount { get; set; }

    [XmlElement("Clip")]
    public List<ClipExportDto> Clips { get; set; } = [];

    [XmlElement("Collection")]
    public List<CollectionExportDto> Collections { get; set; } = [];
}
