using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Models.Import;
using ClipMate.Core.Services.Abstractions;
using FakeItEasy;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for entering ClipMate 7 encryption keys in the ClipMate 7 import dialog.
/// </summary>
public class ClipMate7ImportViewModelTests
{
    private const string _sourceFolder = @"C:\ClipMate7";

    private IClipMate7ImportService _importService = null!;
    private ClipMate7ImportViewModel _viewModel = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _importService = A.Fake<IClipMate7ImportService>();
        A.CallTo(() => _importService.IsClipMate7Database(_sourceFolder)).Returns(true);
        A.CallTo(() => _importService.AnalyzeAsync(_sourceFolder, A<CancellationToken>.Ignored))
            .Returns(new ClipMate7ImportPreview { ClipCount = 10, EncryptedClipCount = 3 });

        _viewModel = new ClipMate7ImportViewModel(_importService, [new ClipMate7ImportTarget("My Clips", @"C:\db\clipmate.db")])
        {
            IsMerge = true,
        };

        await _viewModel.SetSourceFolderAsync(_sourceFolder);
    }

    [Test]
    public async Task SetSourceFolderAsync_EncryptedClips_ShowsKeyEntry()
    {
        // Assert
        await Assert.That(_viewModel.HasEncryptedClips).IsTrue();
        await Assert.That(_viewModel.EncryptedClipCount).IsEqualTo(3);
    }

    [Test]
    public async Task AddEncryptionKey_SeveralKeys_ReportsHowManyEncryptedClipsTheyOpen()
    {
        // Arrange
        A.CallTo(() => _importService.CountDecryptableClipsAsync(_sourceFolder, A<IReadOnlyList<string>>.That.Matches(p => p.Count == 2), A<CancellationToken>.Ignored))
            .Returns(2);

        // Act
        _viewModel.EncryptionKey = "first key";
        await _viewModel.AddEncryptionKeyCommand.ExecuteAsync(null);
        _viewModel.EncryptionKey = "second key";
        await _viewModel.AddEncryptionKeyCommand.ExecuteAsync(null);
        _viewModel.EncryptionKey = "first key"; // Duplicates are not added twice
        await _viewModel.AddEncryptionKeyCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.EncryptionKey).IsEmpty();
        await Assert.That(_viewModel.EncryptionKeysStatus).Contains("2 key(s) entered");
        await Assert.That(_viewModel.EncryptionKeysStatus).Contains("open 2 of 3");
        await Assert.That(_viewModel.EncryptionKeysStatus).Contains("skipped");
    }

    [Test]
    public async Task ExecuteImport_PassesAddedKeysAndTheKeyBeingTyped()
    {
        // Arrange
        ClipMate7ImportOptions? options = null;
        A.CallTo(() => _importService.ImportAsync(A<ClipMate7ImportOptions>.Ignored, A<Action<ExportProgressMessage>?>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((ClipMate7ImportOptions p, Action<ExportProgressMessage>? _, CancellationToken _) => options = p)
            .Returns(new ClipMate7ImportResult());

        _viewModel.EncryptionKey = "first key";
        await _viewModel.AddEncryptionKeyCommand.ExecuteAsync(null);
        _viewModel.EncryptionKey = "typed, not added";

        // Act
        await _viewModel.ExecuteImportCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(options).IsNotNull();
        await Assert.That(options!.EncryptionKeys).IsEquivalentTo(["first key", "typed, not added"]);
    }

    [Test]
    public async Task ClearEncryptionKeys_ForgetsTheKeys()
    {
        // Arrange
        _viewModel.EncryptionKey = "first key";
        await _viewModel.AddEncryptionKeyCommand.ExecuteAsync(null);

        ClipMate7ImportOptions? options = null;
        A.CallTo(() => _importService.ImportAsync(A<ClipMate7ImportOptions>.Ignored, A<Action<ExportProgressMessage>?>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((ClipMate7ImportOptions p, Action<ExportProgressMessage>? _, CancellationToken _) => options = p)
            .Returns(new ClipMate7ImportResult());

        // Act
        _viewModel.ClearEncryptionKeysCommand.Execute(null);
        await _viewModel.ExecuteImportCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_viewModel.EncryptionKeysStatus).IsEmpty();
        await Assert.That(options!.EncryptionKeys.Where(p => p.Length > 0)).IsEmpty();
    }
}
