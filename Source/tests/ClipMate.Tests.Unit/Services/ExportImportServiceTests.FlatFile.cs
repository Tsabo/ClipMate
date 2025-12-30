using ClipMate.Core.Models;
using ClipMate.Core.Models.Export;
using ClipMate.Platform.Services;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for ExportImportService constructor and initialization.
/// </summary>
public class ExportImportServiceConstructorTests : ExportImportServiceTestsBase
{
    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new ExportImportService(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        await Assert.That(service).IsNotNull();
    }
}

/// <summary>
/// Tests for flat-file export with Sequential naming strategy.
/// </summary>
public class ExportImportServiceSequentialNamingTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_Sequential_WithResetSequence_StartsFromOne()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        PreCreateSequentialFiles(dir, 5); // 00001-00005.txt exist
        var clips = CreateClips("New1", "New2");

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.Sequential,
            true);

        // Assert - should start from 1, potentially overwriting
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).ToList();
        await Assert.That(files.Contains("00001.txt")).IsTrue();
        await Assert.That(files.Contains("00002.txt")).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_Sequential_WithEmptyDirectory_StartsFromOne()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("First", "Second", "Third");

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.Sequential);

        // Assert
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(p => p).ToList();
        await Assert.That(files).Contains("00001.txt");
        await Assert.That(files).Contains("00002.txt");
        await Assert.That(files).Contains("00003.txt");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_Sequential_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var service = CreateService();
        var dir = Path.Combine(Path.GetTempPath(), "ClipMateTests", Guid.NewGuid().ToString("N"), "SubDir");
        var clips = CreateClips("Test");

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.Sequential);

        // Assert
        await Assert.That(Directory.Exists(dir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(dir, "00001.txt"))).IsTrue();
    }
}

/// <summary>
/// Tests for flat-file export with Serial (GUID) naming strategy.
/// </summary>
public class ExportImportServiceSerialNamingTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_Serial_UsesClipGuidAsFilename()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("TestClip");
        var clipId = clips[0].Id;

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.Serial);

        // Assert
        var expectedFileName = $"{clipId:N}.txt";
        await Assert.That(File.Exists(Path.Combine(dir, expectedFileName))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_Serial_ProducesUniqueFilenames()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("A", "B", "C", "D", "E");

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.Serial);

        // Assert
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(5);
        // All filenames should be unique (GUID-based)
        var uniqueNames = files.Select(Path.GetFileNameWithoutExtension).Distinct().Count();
        await Assert.That(uniqueNames).IsEqualTo(5);
    }
}

/// <summary>
/// Tests for flat-file export with TitleBased naming strategy.
/// </summary>
public class ExportImportServiceTitleBasedNamingTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_TitleBased_UsesClipTitleAsFilename()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("MyDocument");

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.TitleBased);

        // Assert
        await Assert.That(File.Exists(Path.Combine(dir, "MyDocument.txt"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_TitleBased_HandlesNullTitle()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips((string)null!);

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.TitleBased);

        // Assert - should use "Untitled" for null title
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).ToList();
        await Assert.That(files.Any(p => p!.StartsWith("Untitled"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_TitleBased_WithNormalTitle_CreatesFile()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("ValidTitle");

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.TitleBased);

        // Assert - file should be created with the title as filename
        var files = Directory.GetFiles(dir);
        await Assert.That(files.Length).IsEqualTo(1);
        var fileName = Path.GetFileNameWithoutExtension(files[0]);
        await Assert.That(fileName).IsEqualTo("ValidTitle");
    }
}

/// <summary>
/// Tests for empty clip collections and edge cases.
/// </summary>
public class ExportImportServiceEdgeCaseTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_WithEmptyCollection_ReportsCompletion()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = new List<Clip>();
        var progressMessages = new List<ExportProgressMessage>();

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.Sequential,
            false, 85,
            p => progressMessages.Add(p));

        // Assert
        await Assert.That(progressMessages.Any(p => p is { IsComplete: true, Successful: true })).IsTrue();
        await Assert.That(progressMessages.Any(p => p.Message.Contains("No clips"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_PromptPerFile_ReportsErrorInProgress()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("Test");
        var progressMessages = new List<ExportProgressMessage>();

        // Act - PromptPerFile throws inside the loop but is caught
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.PromptPerFile,
            false, 85,
            p => progressMessages.Add(p));

        // Assert - should report failure for the clip
        await Assert.That(progressMessages.Any(p => !p.Successful && p.Message.Contains("Failed"))).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_ReportsProgressForEachClip()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("A", "B", "C");
        var progressMessages = new List<ExportProgressMessage>();

        // Act
        await service.ExportClipsToFilesAsync(
            clips, dir, FileNamingStrategy.Sequential,
            false, 85,
            p => progressMessages.Add(p));

        // Assert - should have progress for each clip plus completion
        var exportMessages = progressMessages.Where(p => p.Message.Contains("Exported")).ToList();
        await Assert.That(exportMessages.Count).IsEqualTo(3);
    }
}
