using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using DialogResult = ClipMate.Core.Models.DialogResult;

namespace ClipMate.Tests.Unit.ViewModels;

public class AdvancedOptionsViewModelTests
{
    private readonly ClipMateConfiguration _configuration = new();
    private readonly IConfigurationService _configurationService = A.Fake<IConfigurationService>();
    private readonly IDialogService _dialogService = A.Fake<IDialogService>();
    private readonly IApplicationProfileService _profileService = A.Fake<IApplicationProfileService>();
    private readonly IStartupManager _startupManager = A.Fake<IStartupManager>();
    private readonly AdvancedOptionsViewModel _viewModel;

    public AdvancedOptionsViewModelTests()
    {
        A.CallTo(() => _configurationService.Configuration).Returns(_configuration);
        A.CallTo(() => _startupManager.DisableAsync()).Returns((true, (string?)null));
        AnswerConfirmation(DialogResult.Yes);

        _viewModel = new AdvancedOptionsViewModel(_configurationService, _dialogService, _profileService, _startupManager,
            A.Fake<ILogger<AdvancedOptionsViewModel>>());
    }

    private void AnswerConfirmation(DialogResult answer) =>
        A.CallTo(() => _dialogService.ShowMessage(A<string>._, A<string>._, DialogButton.YesNo, A<DialogIcon>._)).Returns(answer);

    private void VerifyNoSuccessMessage() =>
        A.CallTo(() => _dialogService.ShowMessage(A<string>._, A<string>._, DialogButton.OK, DialogIcon.Information)).MustNotHaveHappened();

    [Test]
    public async Task ResetLayoutFont_WhenConfirmed_SavesTheRequestAndTellsTheUserToRestart()
    {
        // Act
        await _viewModel.ResetLayoutFontCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.LayoutAndFonts);
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _dialogService.ShowMessage(A<string>.That.Contains("restart"), A<string>._, DialogButton.OK, DialogIcon.Information))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _profileService.DeleteAllProfilesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ResetApplicationProfile_WhenConfirmed_ClearsAtOnceAndLeavesSettingsAlone()
    {
        // Arrange
        var cleared = false;
        _viewModel.ApplicationProfilesCleared += (_, _) => cleared = true;

        // Act
        await _viewModel.ResetApplicationProfileCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(cleared).IsTrue();
        await Assert.That(_configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.None);
        A.CallTo(() => _profileService.DeleteAllProfilesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _dialogService.ShowMessage(A<string>.That.Contains("has been cleared"), A<string>._, DialogButton.OK, DialogIcon.Information))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ResetEverything_WhenConfirmed_DoesAllThreePartsAndSaysWhatIsKept()
    {
        // Arrange
        var turnedOff = false;
        _viewModel.StartWithWindowsTurnedOff += (_, _) => turnedOff = true;

        // Act
        await _viewModel.ResetEverythingCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(turnedOff).IsTrue();
        await Assert.That(_configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.Everything);
        A.CallTo(() => _profileService.DeleteAllProfilesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _startupManager.DisableAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _dialogService.ShowMessage(A<string>.That.Contains("Your Databases"), A<string>._, DialogButton.YesNo, DialogIcon.Warning))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task EveryReset_WhenDeclined_ChangesNothing()
    {
        // Arrange
        AnswerConfirmation(DialogResult.No);

        // Act
        await _viewModel.ResetLayoutFontCommand.ExecuteAsync(null);
        await _viewModel.ResetApplicationProfileCommand.ExecuteAsync(null);
        await _viewModel.ResetEverythingCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.None);
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _profileService.DeleteAllProfilesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _startupManager.DisableAsync()).MustNotHaveHappened();
        VerifyNoSuccessMessage();
    }

    [Test]
    public async Task Reset_WhenTheSaveFails_ReportsItAndDropsTheRequest()
    {
        // Arrange
        A.CallTo(() => _configurationService.SaveAsync(A<CancellationToken>._)).ThrowsAsync(new IOException("disk full"));

        // Act
        await _viewModel.ResetLayoutFontCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.None);
        A.CallTo(() => _dialogService.ShowMessage(A<string>.That.Contains("disk full"), A<string>._, DialogButton.OK, DialogIcon.Error))
            .MustHaveHappenedOnceExactly();
        VerifyNoSuccessMessage();
    }

    [Test]
    public async Task ResetEverything_WhenStartWithWindowsCannotBeTurnedOff_RequestsNoResetAndReportsIt()
    {
        // Arrange
        A.CallTo(() => _startupManager.DisableAsync()).Returns((false, (string?)"Access denied"));

        // Act
        await _viewModel.ResetEverythingCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(_configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.None);
        A.CallTo(() => _dialogService.ShowMessage(A<string>.That.Contains("Access denied"), A<string>._, DialogButton.OK, DialogIcon.Error))
            .MustHaveHappenedOnceExactly();
        VerifyNoSuccessMessage();
    }
}
