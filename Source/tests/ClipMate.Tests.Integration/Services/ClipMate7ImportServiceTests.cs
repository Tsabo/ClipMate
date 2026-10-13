using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Import;
using ClipMate.Data;
using ClipMate.Data.Legacy.Dbisam;
using ClipMate.Data.Services;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for the ClipMate 7 (DBISAM) reader and importer.
/// The fixture is the stock ClipMate 7 database: 5 sample clips, 4 regular and 8 virtual collections, 1 shortcut.
/// </summary>
[NotInParallel]
public class ClipMate7ImportServiceTests
{
    private static readonly Guid _welcomeClipId = Guid.Parse("56DF8FED-5E03-4533-B769-6CA26C22A6CC");
    private static readonly Guid _htmlClipId = Guid.Parse("F6B8F72A-B671-445C-A9FB-198D4EE336DB");
    private static readonly Guid _macroClipId = Guid.Parse("897CE1B4-7400-4BA4-9568-B705DB4BA9B5");
    private static readonly Guid _inboxId = Guid.Parse("E21B62F2-4CFA-4913-9B79-4F955F4F202D");
    private static readonly Guid _samplesId = Guid.Parse("253CB828-DF18-4833-ACF8-304DF0511122");

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "ClipMate7");

    private string _targetPath = null!;

    [Before(Test)]
    public void Setup() => _targetPath = Path.Combine(Path.GetTempPath(), $"cm7import_{Guid.NewGuid():N}.db");

    [After(Test)]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_targetPath))
            File.Delete(_targetPath);
    }

    private static ClipMate7ImportService CreateService() =>
        new(new SetupService(A.Fake<ILogger<SetupService>>()), A.Fake<ILogger<ClipMate7ImportService>>());

    private ClipMateDbContext CreateTargetContext() =>
        new(new DbContextOptionsBuilder<ClipMateDbContext>().UseSqlite($"Data Source={_targetPath}").Options);

    private async Task CreateEmptyTargetAsync()
    {
        await using var context = CreateTargetContext();
        await context.Database.EnsureCreatedAsync();
    }

    // ==================== DBISAM reader ====================

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task DbisamTable_ClipTable_ParsesSchemaAndRecordCount()
    {
        using var table = DbisamTable.Open(FixtureFolder, "clip");

        string[] expected =
        [
            "ID", "COLLECTION_ID", "TITLE", "CREATOR", "TIMESTAMP", "SORTKEY", "SOURCEURL", "CUSTOMTITLE", "LOCALE",
            "WRAPCHECK", "ENCRYPTED", "ICONS", "DEL", "SIZE", "DELDATE", "USER_ID", "CHECKSUM", "VIEWTAB", "MACRO",
            "CLIP_GUID", "COLL_GUID", "LASTMODIFIED",
        ];

        await Assert.That(table.Fields.Select(p => p.Name)).IsEquivalentTo(expected);
        await Assert.That(table.ActiveRecordCount).IsEqualTo(5);
        await Assert.That(table.ReadRecords().Count()).IsEqualTo(5);
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task DbisamTable_FirstClipRecord_DecodesAllFieldTypes()
    {
        using var table = DbisamTable.Open(FixtureFolder, "clip");
        var record = table.ReadRecords().First();

        await Assert.That(record.GetInt32("ID")).IsEqualTo(1);
        await Assert.That(record.GetString("TITLE")).IsEqualTo("Welcome to ClipMate 7! (Sample Clip)");
        await Assert.That(record.GetGuid("CLIP_GUID")).IsEqualTo(_welcomeClipId);
        await Assert.That(record.GetInt32("SORTKEY")).IsEqualTo(100);
        await Assert.That(record.GetBoolean("ENCRYPTED")).IsFalse();
        await Assert.That(record.GetDateTime("TIMESTAMP")).IsEqualTo(new DateTime(2025, 12, 14, 14, 13, 44, 937, DateTimeKind.Local));
        await Assert.That(record.GetString("NOT_A_FIELD")).IsNull();
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task DbisamTable_MultiBlockMemo_ReadsWholeBlob()
    {
        using var table = DbisamTable.Open(FixtureFolder, "blobtxt");
        var record = table.ReadRecords().Single(p => p.GetInt32("CLIPDATA_ID") == 1);

        var data = record.GetBlob("DATA");

        // 561 bytes spans three 256-byte blocks
        await Assert.That(data.Length).IsEqualTo(561);
        await Assert.That(DbisamTable.Ansi.GetString(data)).StartsWith("Welcome to ClipMate 7!");
        await Assert.That(DbisamTable.Ansi.GetString(data).TrimEnd()).EndsWith("http://www.thornsoft.com");
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task DbisamTable_NonDbisamFile_ThrowsFormatException()
    {
        var folder = Directory.CreateTempSubdirectory("cm7bad").FullName;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, "clip.dat"), new byte[0x400]);

            await Assert.That(() => DbisamTable.Open(folder, "clip")).Throws<DbisamFormatException>();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    // ==================== Import service ====================

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task AnalyzeAsync_Fixture_ReportsCounts()
    {
        var preview = await CreateService().AnalyzeAsync(FixtureFolder);

        await Assert.That(preview.ClipCount).IsEqualTo(5);
        await Assert.That(preview.CollectionCount).IsEqualTo(12);
        await Assert.That(preview.FormatCount).IsEqualTo(6);
        await Assert.That(preview.ShortcutCount).IsEqualTo(1);
        await Assert.That(preview.EncryptedClipCount).IsEqualTo(0);
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task AnalyzeAsync_FolderWithoutDatabase_Throws()
    {
        var service = CreateService();
        var folder = Directory.CreateTempSubdirectory("cm7empty").FullName;
        try
        {
            await Assert.That(service.IsClipMate7Database(folder)).IsFalse();
            await Assert.That(async () => await service.AnalyzeAsync(folder)).Throws<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task ImportAsync_IntoEmptyDatabase_ImportsClipsCollectionsAndShortcuts()
    {
        await CreateEmptyTargetAsync();

        var result = await CreateService().ImportAsync(new ClipMate7ImportOptions
        {
            SourceFolder = FixtureFolder,
            TargetDatabasePath = _targetPath,
        });

        await Assert.That(result.ClipsImported).IsEqualTo(5);
        await Assert.That(result.CollectionsImported).IsEqualTo(4); // InBox, Safe, Overflow, Samples
        await Assert.That(result.VirtualCollectionsSkipped).IsEqualTo(8);
        await Assert.That(result.ShortcutsImported).IsEqualTo(1);
        await Assert.That(result.UnsupportedFormatsSkipped).IsEqualTo(0);

        await using var context = CreateTargetContext();

        var welcome = await context.Clips.SingleAsync(p => p.Id == _welcomeClipId);
        await Assert.That(welcome.Title).IsEqualTo("Welcome to ClipMate 7! (Sample Clip)");
        await Assert.That(welcome.CollectionId).IsEqualTo(_inboxId);
        await Assert.That(welcome.SortKey).IsEqualTo(100);
        await Assert.That(welcome.Type).IsEqualTo(ClipType.Text);
        await Assert.That(welcome.ContentHash).IsNotEmpty();

        var welcomeText = await context.BlobTxt.SingleAsync(p => p.ClipId == _welcomeClipId);
        await Assert.That(welcomeText.Data).StartsWith("Welcome to ClipMate 7!");

        var html = await context.Clips.SingleAsync(p => p.Id == _htmlClipId);
        await Assert.That(html.Type).IsEqualTo(ClipType.Html);
        await Assert.That(html.CollectionId).IsEqualTo(_samplesId);

        var htmlFormat = await context.ClipData.SingleAsync(p => p.ClipId == _htmlClipId && p.Format == Formats.Html.Code);
        var htmlBlob = await context.BlobTxt.SingleAsync(p => p.ClipDataId == htmlFormat.Id);
        await Assert.That(htmlBlob.Data).StartsWith("Version:0.9");
        await Assert.That(htmlBlob.Data).DoesNotEndWith("\0");

        var macro = await context.Clips.SingleAsync(p => p.Id == _macroClipId);
        await Assert.That(macro.Macro).IsTrue();

        var shortcut = await context.Shortcuts.SingleAsync();
        await Assert.That(shortcut.Nickname).IsEqualTo(".s.welcome");
        await Assert.That(shortcut.ClipId).IsEqualTo(_welcomeClipId);

        var samples = await context.Collections.SingleAsync(p => p.Id == _samplesId);
        await Assert.That(samples.LmType).IsEqualTo(CollectionLmType.Folder);
        await Assert.That(await context.Collections.AnyAsync(p => p.LmType == CollectionLmType.Virtual)).IsFalse();
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task ImportAsync_NewDatabase_MatchesDefaultCollectionsInsteadOfDuplicating()
    {
        var result = await CreateService().ImportAsync(new ClipMate7ImportOptions
        {
            SourceFolder = FixtureFolder,
            TargetDatabasePath = _targetPath,
            CreateNewDatabase = true,
        });

        // The default database already holds the ClipMate 7 sample clips (same GUIDs), and the fixture's
        // "Safe" (different GUID) is matched to the default "Safe" by title.
        await Assert.That(result.CollectionsImported).IsEqualTo(0);
        await Assert.That(result.DuplicateClipsSkipped).IsEqualTo(5);

        await using var context = CreateTargetContext();
        await Assert.That(await context.Collections.CountAsync(p => p.Title == "Safe")).IsEqualTo(1);
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task ImportAsync_Twice_SecondImportAddsNothing()
    {
        await CreateEmptyTargetAsync();
        var service = CreateService();
        var options = new ClipMate7ImportOptions { SourceFolder = FixtureFolder, TargetDatabasePath = _targetPath };

        await service.ImportAsync(options);
        var second = await service.ImportAsync(options);

        await Assert.That(second.ClipsImported).IsEqualTo(0);
        await Assert.That(second.CollectionsImported).IsEqualTo(0);
        await Assert.That(second.ShortcutsImported).IsEqualTo(0);
        await Assert.That(second.DuplicateClipsSkipped).IsEqualTo(5);

        await using var context = CreateTargetContext();
        await Assert.That(await context.Clips.CountAsync()).IsEqualTo(5);
    }

    [Test]
    [Category("Integration")]
    [Category("ClipMate7Import")]
    public async Task ImportAsync_NewDatabaseCancelled_RemovesDatabaseFile()
    {
        using var cts = new CancellationTokenSource();
        var options = new ClipMate7ImportOptions
        {
            SourceFolder = FixtureFolder,
            TargetDatabasePath = _targetPath,
            CreateNewDatabase = true,
        };

        // Cancel once the database has been created and the import itself has started
        await Assert.That(async () => await CreateService().ImportAsync(options, p =>
            {
                if (p.Message.StartsWith("Importing collections", StringComparison.Ordinal))
                    cts.Cancel();
            }, cts.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(File.Exists(_targetPath)).IsFalse();
    }
}
