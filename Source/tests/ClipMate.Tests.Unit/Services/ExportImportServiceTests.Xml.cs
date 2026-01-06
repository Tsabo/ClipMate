using System.Xml.Serialization;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Export;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

public class ExportImportServiceXmlTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_ProducesSerializableFile_WithCorrectCounts()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var clips = CreateClips("One", "Two");
        var collections = new List<Collection>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Main",
                LmType = CollectionLmType.Normal,
                ListType = CollectionListType.Normal,
            },
        };

        var progressMessages = new List<ExportProgressMessage>();

        // Act
        await service.ExportToXmlAsync(clips, collections, xmlPath, p => progressMessages.Add(p));

        // Assert
        await Assert.That(File.Exists(xmlPath)).IsTrue();

        // Deserialize back and verify counts
        var serializer = new XmlSerializer(typeof(XmlExportData));
        await using var fs = File.OpenRead(xmlPath);
        var data = (XmlExportData?)serializer.Deserialize(fs);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.ClipCount).IsEqualTo(clips.Count);
        await Assert.That(data.CollectionCount).IsEqualTo(collections.Count);
        await Assert.That(data.Clips.Count).IsEqualTo(clips.Count);
        await Assert.That(data.Collections.Count).IsEqualTo(collections.Count);

        // Progress should include completion
        await Assert.That(progressMessages.Any(p => p is { IsComplete: true, Successful: true })).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ImportFromXml_ReadsExportedFile_ProducesImportData()
    {
        // Arrange: create XML via export
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var clips = CreateClips("Alpha", "Beta", "Gamma");
        var collections = new List<Collection>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Main",
                LmType = CollectionLmType.Normal,
                ListType = CollectionListType.Normal,
            },
        };

        await service.ExportToXmlAsync(clips, collections, xmlPath);

        // Act
        var import = await service.ImportFromXmlAsync(xmlPath, collections[0].Id);

        // Assert
        await Assert.That(import).IsNotNull();
        await Assert.That(import.SourceFile).IsEqualTo(xmlPath);
        await Assert.That(import.ClipCount).IsEqualTo(clips.Count);
        await Assert.That(import.CollectionCount).IsEqualTo(collections.Count);
        await Assert.That(import.TargetCollectionId).IsEqualTo(collections[0].Id);
        await Assert.That(import.Clips.Count).IsEqualTo(clips.Count);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_WithImageData_PreservesImageContent()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var imageClip = CreateImageClip("Test Image");
        var clips = new List<Clip> { imageClip };

        // Act: Export with image
        await service.ExportToXmlAsync(clips, [], xmlPath);

        // Re-import
        var import = await service.ImportFromXmlAsync(xmlPath);

        // Assert: Image data should be preserved
        await Assert.That(import.Clips.Count).IsEqualTo(1);
        var importedClip = import.Clips[0];
        await Assert.That(importedClip).IsNotNull();
        await Assert.That(importedClip.Type).IsEqualTo(ClipType.Image);
        await Assert.That(importedClip.ImageData).IsNotNull();
        await Assert.That(importedClip.ImageData).IsEquivalentTo(imageClip.ImageData);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_WithFilePathsJson_PreservesFileContent()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var filePaths = new[] { "C:\\file1.txt", "C:\\file2.pdf" };
        var filesClip = CreateFilesClip(filePaths, "Test Files");
        var clips = new List<Clip> { filesClip };

        // Act: Export with file paths
        await service.ExportToXmlAsync(clips, [], xmlPath);

        // Re-import
        var import = await service.ImportFromXmlAsync(xmlPath);

        // Assert: File paths should be preserved
        await Assert.That(import.Clips.Count).IsEqualTo(1);
        var importedClip = import.Clips[0];
        await Assert.That(importedClip.Type).IsEqualTo(ClipType.Files);
        await Assert.That(importedClip.FilePathsJson).IsNotNull();
        await Assert.That(importedClip.FilePathsJson?.Replace("\r\n", "\n")).IsEqualTo(filesClip.FilePathsJson?.Replace("\r\n", "\n"));
    }
}
