using System.Xml.Serialization;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Export;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Additional XML export tests for edge cases and error handling.
/// </summary>
public class ExportImportServiceXmlEdgeCaseTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_WithEmptyClips_ProducesValidXml()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");

        // Act
        await service.ExportToXmlAsync([], [], xmlPath);

        // Assert
        await Assert.That(File.Exists(xmlPath)).IsTrue();
        var serializer = new XmlSerializer(typeof(XmlExportData));
        await using var fs = File.OpenRead(xmlPath);
        var data = (XmlExportData?)serializer.Deserialize(fs);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.ClipCount).IsEqualTo(0);
        await Assert.That(data.CollectionCount).IsEqualTo(0);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_PreservesClipProperties()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var clipId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var clips = new List<Clip>
        {
            new()
            {
                Id = clipId,
                CollectionId = collectionId,
                Title = "Test Title",
                Creator = "TestUser",
                CapturedAt = new DateTimeOffset(2025, 12, 28, 10, 30, 0, TimeSpan.Zero),
                ContentHash = "abc123",
                Type = ClipType.Text,
                IsFavorite = true,
                PasteCount = 5,
                Label = "Important",
            },
        };

        // Act
        await service.ExportToXmlAsync(clips, [], xmlPath);

        // Assert - reimport and verify properties preserved
        var import = await service.ImportFromXmlAsync(xmlPath);
        var importedClip = import.Clips.First();
        await Assert.That(importedClip.Id).IsEqualTo(clipId);
        await Assert.That(importedClip.CollectionId).IsEqualTo(collectionId);
        await Assert.That(importedClip.Title).IsEqualTo("Test Title");
        await Assert.That(importedClip.Creator).IsEqualTo("TestUser");
        await Assert.That(importedClip.ContentHash).IsEqualTo("abc123");
        await Assert.That(importedClip.Type).IsEqualTo(ClipType.Text);
        await Assert.That(importedClip.IsFavorite).IsTrue();
        await Assert.That(importedClip.PasteCount).IsEqualTo(5);
        await Assert.That(importedClip.Label).IsEqualTo("Important");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_PreservesCollectionProperties()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var collectionId = Guid.NewGuid();
        var collections = new List<Collection>
        {
            new()
            {
                Id = collectionId,
                Title = "My Collection",
                LmType = CollectionLmType.Normal,
                ListType = CollectionListType.Normal,
                SortKey = 100,
                RetentionLimit = 500,
                AcceptNewClips = true,
                AcceptDuplicates = false,
                ReadOnly = false,
                Favorite = true,
                Description = "Test description",
            },
        };

        // Act
        await service.ExportToXmlAsync([], collections, xmlPath);

        // Assert - reimport and verify properties preserved
        var import = await service.ImportFromXmlAsync(xmlPath);
        var importedColl = import.Collections.First();
        await Assert.That(importedColl.Id).IsEqualTo(collectionId);
        await Assert.That(importedColl.Title).IsEqualTo("My Collection");
        await Assert.That(importedColl.LmType).IsEqualTo(CollectionLmType.Normal);
        await Assert.That(importedColl.RetentionLimit).IsEqualTo(500);
        await Assert.That(importedColl.AcceptNewClips).IsTrue();
        await Assert.That(importedColl.Favorite).IsTrue();
        await Assert.That(importedColl.Description).IsEqualTo("Test description");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ImportFromXml_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var service = CreateService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");

        // Act & Assert
        await Assert.That(async () => await service.ImportFromXmlAsync(nonExistentPath))
            .Throws<FileNotFoundException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ImportFromXml_WithNoTargetCollection_UsesEmptyGuid()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        await service.ExportToXmlAsync(CreateClips("Test"), new List<Collection>(), xmlPath);

        // Act
        var import = await service.ImportFromXmlAsync(xmlPath);

        // Assert
        await Assert.That(import.TargetCollectionId).IsEqualTo(Guid.Empty);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_SetsVersionAndTimestamp()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var beforeExport = DateTime.UtcNow;

        // Act
        await service.ExportToXmlAsync(CreateClips("Test"), new List<Collection>(), xmlPath);

        // Assert
        var serializer = new XmlSerializer(typeof(XmlExportData));
        await using var fs = File.OpenRead(xmlPath);
        var data = (XmlExportData?)serializer.Deserialize(fs);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Version).IsEqualTo("1.0");
        await Assert.That(data.ExportedAt).IsGreaterThanOrEqualTo(beforeExport);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ImportFromXml_SetsImportTimestamp()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        await service.ExportToXmlAsync(CreateClips("Test"), new List<Collection>(), xmlPath);
        var beforeImport = DateTime.UtcNow;

        // Act
        var import = await service.ImportFromXmlAsync(xmlPath);

        // Assert
        await Assert.That(import.ImportedAt).IsGreaterThanOrEqualTo(beforeImport);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_ReportsProgress()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var progressMessages = new List<ExportProgressMessage>();

        // Act
        await service.ExportToXmlAsync(
            CreateClips("A", "B"),
            new List<Collection>(),
            xmlPath,
            p => progressMessages.Add(p));

        // Assert
        await Assert.That(progressMessages.Count).IsGreaterThanOrEqualTo(2); // Preparing + Complete
        await Assert.That(progressMessages.Any(p => p.Message.Contains("Preparing"))).IsTrue();
        await Assert.That(progressMessages.Any(p => p is { IsComplete: true, Successful: true })).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ImportFromXml_ReportsProgress()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        await service.ExportToXmlAsync(CreateClips("Test"), new List<Collection>(), xmlPath);
        var progressMessages = new List<ExportProgressMessage>();

        // Act
        await service.ImportFromXmlAsync(xmlPath, progressCallback: p => progressMessages.Add(p));

        // Assert
        await Assert.That(progressMessages.Count).IsGreaterThanOrEqualTo(2); // Reading + Ready
        await Assert.That(progressMessages.Any(p => p.Message.Contains("Reading"))).IsTrue();
        await Assert.That(progressMessages.Any(p => p is { IsComplete: true, Successful: true })).IsTrue();
    }
}
