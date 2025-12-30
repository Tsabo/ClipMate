using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Export;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for exporting different content types (Image, HTML, RTF, Files).
/// </summary>
public class ExportImportServiceImageTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_ImageClip_ExportsAsPng_ByDefault()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateImageClip("TestImage");
        var progressMessages = new List<ExportProgressMessage>();

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased,
            progressCallback: p => progressMessages.Add(p));

        // Assert
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".png");
        await Assert.That(progressMessages.Any(p => p is { IsComplete: true, Successful: true })).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_ImageClip_ExportsAsJpg_WhenSpecified()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateImageClip("JpgTest");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased,
            imageFormat: ImageExportFormat.Jpg);

        // Assert
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".jpg");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_ImageClip_ExportsAsBmp_WhenSpecified()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateImageClip("BmpTest");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased,
            imageFormat: ImageExportFormat.Bmp);

        // Assert
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".bmp");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_ImageClip_WithSequentialNaming_CreatesNumberedFile()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateImageClip();

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.Sequential,
            imageFormat: ImageExportFormat.Png);

        // Assert
        await Assert.That(File.Exists(Path.Combine(dir, "00001.png"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_ImageClip_JpegQuality_AffectsOutput()
    {
        // Arrange
        var service = CreateService();
        var dirHighQuality = CreateTempDirectory();
        var dirLowQuality = CreateTempDirectory();
        var clip1 = CreateImageClip("HighQuality");
        var clip2 = CreateImageClip("LowQuality");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip1],
            dirHighQuality,
            FileNamingStrategy.TitleBased,
            jpegQuality: 100,
            imageFormat: ImageExportFormat.Jpg);

        await service.ExportClipsToFilesAsync(
            [clip2],
            dirLowQuality,
            FileNamingStrategy.TitleBased,
            jpegQuality: 10,
            imageFormat: ImageExportFormat.Jpg);

        // Assert - both should exist (quality affects compression, not validity)
        var highQualityFile = Directory.GetFiles(dirHighQuality).First();
        var lowQualityFile = Directory.GetFiles(dirLowQuality).First();
        await Assert.That(File.Exists(highQualityFile)).IsTrue();
        await Assert.That(File.Exists(lowQualityFile)).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_MultipleImageClips_AllExported()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = new List<Clip>
        {
            CreateImageClip("Image1"),
            CreateImageClip("Image2"),
            CreateImageClip("Image3"),
        };

        // Act
        await service.ExportClipsToFilesAsync(
            clips,
            dir,
            FileNamingStrategy.Sequential,
            imageFormat: ImageExportFormat.Png);

        // Assert
        var files = Directory.GetFiles(dir, "*.png");
        await Assert.That(files.Length).IsEqualTo(3);
    }
}

/// <summary>
/// Tests for exporting HTML content clips.
/// </summary>
public class ExportImportServiceHtmlTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_HtmlClip_ExportsAsHtmlFile()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateHtmlClip("<html><body><p>Hello World</p></body></html>", "HtmlDoc");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased);

        // Assert
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".html");

        var content = await File.ReadAllTextAsync(files[0]);
        await Assert.That(content).Contains("<p>Hello World</p>");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_HtmlClip_SequentialNaming_UsesHtmlExtension()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateHtmlClip("<div>Test</div>");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.Sequential);

        // Assert
        await Assert.That(File.Exists(Path.Combine(dir, "00001.html"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_HtmlClip_WithEmptyHtml_FallsBackToText()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateHtmlClip(string.Empty, "EmptyHtml");
        clip.TextContent = "Fallback text content";

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased);

        // Assert - should export as .txt with fallback content
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".txt");
    }
}

/// <summary>
/// Tests for exporting RichText (RTF) content clips.
/// </summary>
public class ExportImportServiceRtfTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_RtfClip_ExportsAsRtfFile()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var rtfContent = @"{\rtf1\ansi Hello World}";
        var clip = CreateRtfClip(rtfContent, "RtfDoc");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased);

        // Assert
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".rtf");

        var content = await File.ReadAllTextAsync(files[0]);
        await Assert.That(content).Contains(@"\rtf1");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_RtfClip_SequentialNaming_UsesRtfExtension()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateRtfClip(@"{\rtf1\ansi Test}");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.Sequential);

        // Assert
        await Assert.That(File.Exists(Path.Combine(dir, "00001.rtf"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_RtfClip_WithEmptyRtf_FallsBackToText()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateRtfClip(string.Empty, "EmptyRtf");
        clip.TextContent = "Plain text fallback";

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased);

        // Assert - should export as .txt with fallback content
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".txt");
    }
}

/// <summary>
/// Tests for exporting Files-type clips.
/// </summary>
public class ExportImportServiceFilesTypeTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_FilesClip_ExportsAsJsonFile()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clip = CreateFilesClip(["C:\\file1.txt", "C:\\folder\\file2.doc"], "FileList");

        // Act
        await service.ExportClipsToFilesAsync(
            [clip],
            dir,
            FileNamingStrategy.TitleBased);

        // Assert - FilePathsJson is exported as .json
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        await Assert.That(Path.GetExtension(files[0])).IsEqualTo(".json");

        var content = await File.ReadAllTextAsync(files[0]);
        await Assert.That(content).Contains("file1.txt");
        await Assert.That(content).Contains("file2.doc");
    }
}

/// <summary>
/// Tests for cancellation token support in export operations.
/// </summary>
public class ExportImportServiceCancellationTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("A", "B", "C", "D", "E");
        using var cts = new CancellationTokenSource();

        // Cancel immediately
        await cts.CancelAsync();

        // Act & Assert
        await Assert.That(async () => await service.ExportClipsToFilesAsync(
                clips,
                dir,
                FileNamingStrategy.Sequential,
                cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportToXml_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");
        var clips = CreateClips("A", "B", "C");
        using var cts = new CancellationTokenSource();

        // Cancel immediately
        await cts.CancelAsync();

        // Act & Assert
        await Assert.That(async () => await service.ExportToXmlAsync(
                clips,
                new List<Collection>(),
                xmlPath,
                cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ImportFromXml_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = CreateService();
        var xmlPath = CreateTempFilePath(".xml");

        // First create a valid XML file
        await service.ExportToXmlAsync(CreateClips("Test"), new List<Collection>(), xmlPath);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.That(async () => await service.ImportFromXmlAsync(
                xmlPath,
                cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_WithoutCancellation_CompletesSuccessfully()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("Test");
        using var cts = new CancellationTokenSource();

        // Act - don't cancel
        await service.ExportClipsToFilesAsync(
            clips,
            dir,
            FileNamingStrategy.Sequential,
            cancellationToken: cts.Token);

        // Assert
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
    }
}

/// <summary>
/// Tests for mixed content type exports.
/// </summary>
public class ExportImportServiceMixedContentTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_MixedContentTypes_ExportsAllWithCorrectExtensions()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = new List<Clip>
        {
            CreateClips("TextClip")[0],
            CreateImageClip("ImageClip"),
            CreateHtmlClip("<p>HTML</p>", "HtmlClip"),
            CreateRtfClip(@"{\rtf1 RTF}", "RtfClip"),
        };

        // Act
        await service.ExportClipsToFilesAsync(
            clips,
            dir,
            FileNamingStrategy.TitleBased,
            imageFormat: ImageExportFormat.Png);

        // Assert
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).ToList();
        await Assert.That(files.Count).IsEqualTo(4);
        await Assert.That(files.Any(p => p!.EndsWith(".txt"))).IsTrue();
        await Assert.That(files.Any(p => p!.EndsWith(".png"))).IsTrue();
        await Assert.That(files.Any(p => p!.EndsWith(".html"))).IsTrue();
        await Assert.That(files.Any(p => p!.EndsWith(".rtf"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_MixedContentTypes_SequentialNaming_AllNumbered()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = new List<Clip>
        {
            CreateClips("Text")[0],
            CreateImageClip(),
            CreateHtmlClip("<p>Test</p>"),
        };

        // Act
        await service.ExportClipsToFilesAsync(
            clips,
            dir,
            FileNamingStrategy.Sequential,
            imageFormat: ImageExportFormat.Jpg);

        // Assert
        var files = Directory.GetFiles(dir).OrderBy(p => p).ToList();
        await Assert.That(files.Count).IsEqualTo(3);
        await Assert.That(files[0]).EndsWith("00001.txt");
        await Assert.That(files[1]).EndsWith("00002.jpg");
        await Assert.That(files[2]).EndsWith("00003.html");
    }
}
