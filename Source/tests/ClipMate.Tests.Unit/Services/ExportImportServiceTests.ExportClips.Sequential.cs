using ClipMate.Core.Models.Export;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

public class ExportImportServiceSequentialTests : ExportImportServiceTestsBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_Sequential_RespectsExistingFilesAndIncrements()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        PreCreateSequentialFiles(dir, 2); // 00001.txt, 00002.txt exist
        var clips = CreateClips("A", "B", "C");
        var progressMessages = new List<ExportProgressMessage>();

        // Act
        await service.ExportClipsToFilesAsync(
            clips,
            dir,
            FileNamingStrategy.Sequential,
            false,
            85,
            p => progressMessages.Add(p),
            default);

        // Assert
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(p => p).ToList();
        await Assert.That(files.Contains("00003.txt")).IsTrue();
        await Assert.That(files.Contains("00004.txt")).IsTrue();
        await Assert.That(files.Contains("00005.txt")).IsTrue();

        // Verify file contents correspond to clip titles
        var content3 = await File.ReadAllTextAsync(Path.Combine(dir, "00003.txt"));
        var content4 = await File.ReadAllTextAsync(Path.Combine(dir, "00004.txt"));
        var content5 = await File.ReadAllTextAsync(Path.Combine(dir, "00005.txt"));
        await Assert.That(content3).IsEqualTo("A");
        await Assert.That(content4).IsEqualTo("B");
        await Assert.That(content5).IsEqualTo("C");

        // Progress should include a completion message
        await Assert.That(progressMessages.Any(p => p is { IsComplete: true, Successful: true })).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ExportClipsToFiles_TitleBased_SanitizesInvalidCharacters()
    {
        // Arrange
        var service = CreateService();
        var dir = CreateTempDirectory();
        var clips = CreateClips("My:Title*?", "   ");

        // Act
        await service.ExportClipsToFilesAsync(
            clips,
            dir,
            FileNamingStrategy.TitleBased,
            false,
            85,
            null,
            default);

        // Assert
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).Where(p => p != null).OrderBy(p => p).ToList();
        // Should not contain invalid characters like ':' '*' '?' and blank should become 'Untitled'
        await Assert.That(files.Any(p => p!.Contains(':') || p!.Contains('*') || p!.Contains('?'))).IsFalse();
        await Assert.That(files.Any(p => p!.StartsWith("Untitled") && p!.EndsWith(".txt"))).IsTrue();
    }
}
