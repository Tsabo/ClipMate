using System.Text;
using Windows.Win32.Foundation;
using ClipMate.Core.Constants;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using ClipMate.Tests.Unit.Networking;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;

namespace ClipMate.Tests.Unit.Platform;

/// <summary>
/// Tests for reading the clipboard in one session: every Format in the source's order, with ClipMate 7's retries.
/// </summary>
public class RawClipboardReaderTests
{
    private const uint _privateFormat = 0xC123;
    private const uint _htmlFormat = 0xC0A0;
    private static readonly HWND _window = new(42);

    [Test]
    public async Task ReadAsync_SeveralFormats_KeepsTheSourcesOrderInOneSession()
    {
        // Arrange: the source offers its private format first, then HTML, then Unicode text
        var win32 = FakeClipboard(
            (_privateFormat, "Acme Private", [1, 2, 3]),
            (_htmlFormat, Formats.Html.Name, "<b>x</b>"u8.ToArray()),
            ((uint)Formats.UnicodeText.Code, null, Encoding.Unicode.GetBytes("x\0")));
        var reader = new RawClipboardReader(win32, new ManualTimeProvider(), NullLogger.Instance);

        // Act
        var snapshot = await reader.ReadAsync(_window, honourIgnoreFlag: true);

        // Assert
        await Assert.That(snapshot!.FormatCount).IsEqualTo(3);
        await Assert.That(snapshot.Formats.Select(p => p.Name)).IsEquivalentTo(["Acme Private", Formats.Html.Name, "CF_UNICODETEXT"], CollectionOrdering.Matching);
        await Assert.That(snapshot.Formats[0].Code).IsEqualTo(_privateFormat);
        await Assert.That(snapshot.Formats[0].Data).IsEquivalentTo(new byte[] { 1, 2, 3 });
        A.CallTo(() => win32.OpenClipboard(_window)).MustHaveHappenedOnceExactly();
        A.CallTo(() => win32.CloseClipboard()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ReadAsync_ClipboardWithAnOwner_RecordsTheOwnerWhileTheClipboardIsOpen()
    {
        // Arrange
        var owner = new HWND(77);
        var win32 = FakeClipboard(((uint)Formats.UnicodeText.Code, null, Encoding.Unicode.GetBytes("x\0")));
        A.CallTo(() => win32.GetClipboardOwner()).Returns(owner);
        var reader = new RawClipboardReader(win32, new ManualTimeProvider(), NullLogger.Instance);

        // Act
        var snapshot = await reader.ReadAsync(_window, honourIgnoreFlag: true);

        // Assert
        await Assert.That(snapshot!.Owner).IsEqualTo(owner);
        A.CallTo(() => win32.OpenClipboard(_window)).MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => win32.GetClipboardOwner()).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => win32.CloseClipboard()).MustHaveHappenedOnceExactly());
    }

    [Test]
    public async Task ReadAsync_NeverCaptureAndUnnamedFormats_AreLeftOut()
    {
        // Arrange: ClipMate 7's never-capture list, a format without a name and GDI handle formats around one keeper
        var win32 = FakeClipboard(
            ((uint)Formats.OemText.Code, null, [1]),
            (0x81, null, [1]), // CF_DSPTEXT
            ((uint)Formats.Palette.Code, null, [1]),
            (0xC001, "ClipMate Ping", [1]),
            (0xC002, "Borland IDE Block Type", [1]),
            (0x0200, null, [1]), // private range, no name
            ((uint)Formats.Bitmap.Code, null, [1]),
            ((uint)Formats.Locale.Code, null, [9, 4, 0, 0]));
        var reader = new RawClipboardReader(win32, new ManualTimeProvider(), NullLogger.Instance);

        // Act
        var snapshot = await reader.ReadAsync(_window, honourIgnoreFlag: true);

        // Assert
        await Assert.That(snapshot!.FormatCount).IsEqualTo(8);
        await Assert.That(snapshot.Formats.Select(p => p.Name)).IsEquivalentTo(["CF_LOCALE"]);
        A.CallTo(() => win32.GetClipboardData((uint)Formats.OemText.Code)).MustNotHaveHappened();
        A.CallTo(() => win32.GetClipboardData((uint)Formats.Bitmap.Code)).MustNotHaveHappened();
    }

    [Test]
    public async Task ReadAsync_EmptyClipboard_HasNoFormats()
    {
        var win32 = FakeClipboard();
        var reader = new RawClipboardReader(win32, new ManualTimeProvider(), NullLogger.Instance);

        var snapshot = await reader.ReadAsync(_window, honourIgnoreFlag: true);

        await Assert.That(snapshot!.FormatCount).IsEqualTo(0);
        await Assert.That(snapshot.Formats).IsEmpty();
    }

    [Test]
    public async Task ReadAsync_IgnoreFlag_ReadsNoData()
    {
        // Arrange: a change marked for clipboard viewers to ignore must not make its source render anything
        var win32 = FakeClipboard(
            ((uint)Formats.UnicodeText.Code, null, Encoding.Unicode.GetBytes("x\0")),
            (0xC010, ClipboardIgnoreFormats.ClipboardViewerIgnore, []));
        var reader = new RawClipboardReader(win32, new ManualTimeProvider(), NullLogger.Instance);

        // Act
        var snapshot = await reader.ReadAsync(_window, honourIgnoreFlag: true);

        // Assert
        await Assert.That(snapshot!.IgnoreFlag).IsTrue();
        await Assert.That(snapshot.Formats).IsEmpty();
        A.CallTo(() => win32.GetClipboardData(A<uint>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ReadAsync_IgnoreFlagNotHonoured_ReadsEverythingButTheMarker()
    {
        var win32 = FakeClipboard(
            ((uint)Formats.UnicodeText.Code, null, Encoding.Unicode.GetBytes("x\0")),
            (0xC010, ClipboardIgnoreFormats.ClipboardViewerIgnore, []));
        var reader = new RawClipboardReader(win32, new ManualTimeProvider(), NullLogger.Instance);

        var snapshot = await reader.ReadAsync(_window, honourIgnoreFlag: false);

        await Assert.That(snapshot!.IgnoreFlag).IsFalse();
        await Assert.That(snapshot.Formats.Select(p => p.Name)).IsEquivalentTo(["CF_UNICODETEXT"]);
    }

    [Test]
    public async Task ReadAsync_ReadFailsThenSucceeds_ClosesWaits100MsAndReopens()
    {
        // Arrange: the source fails to render its format twice
        var timeProvider = new ManualTimeProvider();
        var win32 = FakeClipboard((_privateFormat, "Acme Private", [7]));
        A.CallTo(() => win32.GetClipboardData(_privateFormat)).ReturnsNextFromSequence(null, null, [7]);
        var reader = new RawClipboardReader(win32, timeProvider, NullLogger.Instance);

        // Act
        var read = reader.ReadAsync(_window, honourIgnoreFlag: true);
        await Assert.That(read.IsCompleted).IsFalse();
        await AdvanceUntilCompletedAsync(timeProvider, read);
        var snapshot = await read;

        // Assert
        await Assert.That(snapshot!.Formats.Single().Data).IsEquivalentTo(new byte[] { 7 });
        await Assert.That(timeProvider.GetUtcNow() - _start).IsEqualTo(TimeSpan.FromMilliseconds(200));
        A.CallTo(() => win32.OpenClipboard(_window)).MustHaveHappened(3, Times.Exactly);
        A.CallTo(() => win32.CloseClipboard()).MustHaveHappened(3, Times.Exactly);
    }

    [Test]
    public async Task ReadAsync_ReadFailsFourTimes_LeavesTheFormatOutAndKeepsTheRest()
    {
        var timeProvider = new ManualTimeProvider();
        var win32 = FakeClipboard(
            (_privateFormat, "Acme Private", null),
            ((uint)Formats.Locale.Code, null, [9, 4, 0, 0]));
        var reader = new RawClipboardReader(win32, timeProvider, NullLogger.Instance);

        var read = reader.ReadAsync(_window, honourIgnoreFlag: true);
        await AdvanceUntilCompletedAsync(timeProvider, read);
        var snapshot = await read;

        await Assert.That(snapshot!.Formats.Select(p => p.Name)).IsEquivalentTo(["CF_LOCALE"]);
        A.CallTo(() => win32.GetClipboardData(_privateFormat)).MustHaveHappened(4, Times.Exactly);
        await Assert.That(timeProvider.GetUtcNow() - _start).IsEqualTo(TimeSpan.FromMilliseconds(300));
    }

    [Test]
    public async Task ReadAsync_ClipboardStaysBusy_GivesUpAfterFourTries()
    {
        var timeProvider = new ManualTimeProvider();
        var win32 = FakeClipboard(((uint)Formats.Locale.Code, null, [9, 4, 0, 0]));
        A.CallTo(() => win32.OpenClipboard(A<HWND>._)).Returns(false);
        var reader = new RawClipboardReader(win32, timeProvider, NullLogger.Instance);

        var read = reader.ReadAsync(_window, honourIgnoreFlag: true);
        await AdvanceUntilCompletedAsync(timeProvider, read);

        await Assert.That(await read).IsNull();
        A.CallTo(() => win32.OpenClipboard(_window)).MustHaveHappened(4, Times.Exactly);
        A.CallTo(() => win32.CloseClipboard()).MustNotHaveHappened();
    }

    private static readonly DateTimeOffset _start = new ManualTimeProvider().GetUtcNow();

    /// <summary>
    /// A clipboard offering the formats in this order; a null name means a standard format named by ClipMate, or
    /// none for codes it doesn't know; null data means the source never renders it.
    /// </summary>
    private static IWin32ClipboardInterop FakeClipboard(params (uint Code, string? Name, byte[]? Data)[] formats)
    {
        var win32 = A.Fake<IWin32ClipboardInterop>();
        A.CallTo(() => win32.OpenClipboard(A<HWND>._)).Returns(true);
        A.CallTo(() => win32.CloseClipboard()).Returns(true);
        A.CallTo(() => win32.EnumClipboardFormats(A<uint>._)).ReturnsLazily((uint previous) =>
        {
            var index = previous == 0 ? 0 : Array.FindIndex(formats, p => p.Code == previous) + 1;
            return index < formats.Length ? formats[index].Code : 0;
        });
        A.CallTo(() => win32.GetClipboardFormatName(A<uint>._)).ReturnsLazily((uint code) => formats.FirstOrDefault(p => p.Code == code).Name);
        A.CallTo(() => win32.GetClipboardData(A<uint>._)).ReturnsLazily((uint code) => formats.FirstOrDefault(p => p.Code == code).Data);

        return win32;
    }

    private static async Task AdvanceUntilCompletedAsync(ManualTimeProvider timeProvider, Task task)
    {
        for (var i = 0; i < 20 && !task.IsCompleted; i++)
        {
            if (timeProvider.PendingTimers > 0)
                timeProvider.Advance(RawClipboardReader.RetryDelay);
            else
                await Task.Yield();
        }
    }
}
