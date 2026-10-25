using ClipMate.Core.Constants;
using ClipMate.Platform.Helpers;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Helpers;

[NotInParallel(StaTestIsolationTests.StaKey)]
public class OwnClipboardCopyTests
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CreateText_Text_CarriesTheTextAndTheClipboardViewerIgnoreFormatOnly()
    {
        // Act
        var dataObject = OwnClipboardCopy.CreateText("Hello");

        // Assert
        await Assert.That(dataObject.GetData(System.Windows.DataFormats.UnicodeText)).IsEqualTo("Hello");
        await Assert.That(dataObject.GetDataPresent(ClipboardIgnoreFormats.ClipboardViewerIgnore, false)).IsTrue();
        await Assert.That(dataObject.GetDataPresent(ClipboardIgnoreFormats.ExcludeFromMonitorProcessing, false)).IsFalse();
    }
}
