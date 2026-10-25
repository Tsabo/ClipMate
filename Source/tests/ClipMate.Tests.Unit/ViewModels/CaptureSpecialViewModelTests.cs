using ClipMate.App.ViewModels;
using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public class CaptureSpecialViewModelTests
{
    [Test]
    public async Task Formats_AsOnTheClipboard_TextImageRtfAndHtmlStartTicked()
    {
        // Arrange
        var reading = new ClipboardReading(
        [
            new RawClipboardFormat("CF_UNICODETEXT", 13, [0]), new RawClipboardFormat("CF_LOCALE", 16, [0]),
            new RawClipboardFormat("Rich Text Format", 0xC0AC, [0]), new RawClipboardFormat("HTML Format", 0xC12F, [0]),
            new RawClipboardFormat("CF_DIB", 8, [0]), new RawClipboardFormat("Acme Private", 0xC123, [0]),
        ], "NOTEPAD", null);

        // Act
        var viewModel = new CaptureSpecialViewModel(reading);

        // Assert
        await Assert.That(viewModel.Formats.Select(p => p.Name))
            .IsEquivalentTo(["CF_UNICODETEXT", "CF_LOCALE", "Rich Text Format", "HTML Format", "CF_DIB", "Acme Private"]);
        await Assert.That(viewModel.SelectedFormatNames).IsEquivalentTo(["CF_UNICODETEXT", "Rich Text Format", "HTML Format", "CF_DIB"]);
    }

    [Test]
    public async Task SelectedFormatNames_FollowTheTicks()
    {
        var viewModel = new CaptureSpecialViewModel(new ClipboardReading(
            [new RawClipboardFormat("CF_UNICODETEXT", 13, [0]), new RawClipboardFormat("Acme Private", 0xC123, [0])], null, null));

        viewModel.Formats[0].IsSelected = false;
        viewModel.Formats[1].IsSelected = true;

        await Assert.That(viewModel.SelectedFormatNames).IsEquivalentTo(["Acme Private"]);
    }
}
