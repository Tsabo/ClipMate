using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// A screenshot tool that copies the image and its filename together (Screenpresso, SnagIt): ClipMate must keep both
/// Formats and offer both when the clip is picked, so an image-capable Paste Target gets the image and a text-only one
/// gets the filename.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class ClipboardTextAndImageRoundTripTests(AppFixture appFixture)
{
    private const string _probeText = "probe text";

    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _fileName = $"Screenshot {Guid.NewGuid():N}.png";

    [Test]
    [Arguments(true)] // e.g. SnagIt: a bitmap plus the PNG encoding
    [Arguments(false)] // e.g. Screenpresso: a bitmap only, which ClipMate encodes itself
    public async Task ImageWithItsFilename_CapturedAndPicked_OffersBothTheImageAndTheText(bool alsoOfferPng)
    {
        var png = CreatePng();
        var backup = SystemClipboard.Backup();
        try
        {
            TestProgramProfile.Write(appFixture, "CF_DIB");
            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            // Arrange: the image plus its filename as text, as a screenshot tool copies it. The bitmap is created on the
            // clipboard's STA thread: a WPF bitmap can't cross threads unless frozen.
            using var source = await ForegroundTestWindow.OpenAsync();
            source.Copy(() =>
            {
                var copy = new DataObject();
                copy.SetImage(BitmapFrame.Create(new MemoryStream(png)));
                if (alsoOfferPng)
                    copy.SetData("PNG", new MemoryStream(png));

                copy.SetText(_fileName);
                return copy;
            });

            var stored = await Poll.UntilAsync(ReadStoredFormatNamesOfClip, "ClipMate to capture the screenshot");
            await Assert.That(stored).Contains("CF_UNICODETEXT");
            await Assert.That(stored).Contains("DIB"); // the stored image

            // Act: replace the clipboard, then pick the screenshot clip (a text clip that also shows the image icon)
            await clipList.WaitForRowAsync(_fileName);
            var probeAttempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (probeAttempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText(_probeText));

                return clipList.FindRow(_probeText);
            }, "the probe clip in the clip list");

            var row = await clipList.WaitForRowAsync(_fileName);
            await Assert.That(row.Current.Name).Contains(ClipListPage.ImageIcon);
            await clipList.SelectAsync(_fileName);

            var offered = await Poll.UntilAsync(() =>
            {
                var formats = SystemClipboard.TryGetRawFormatNames();
                return formats?.Contains("PNG") == true ? formats : null;
            }, "the screenshot clip to be copied back");

            // Assert: both the image and the text are offered, so the Paste Target can choose
            await Assert.That(offered).Contains("#13"); // CF_UNICODETEXT
            await Assert.That(offered.Any(p => p is "#2" or "#8" or "#17")).IsTrue(); // CF_BITMAP / CF_DIB / CF_DIBV5

            var (text, image) = SystemClipboard.OnSta(() => (Clipboard.GetText(), (Clipboard.GetData("PNG") as MemoryStream)!.ToArray()));
            await Assert.That(text).IsEqualTo(_fileName);
            if (alsoOfferPng)
                await Assert.That(image).IsEquivalentTo(png); // the original encoding, unchanged
            else
                await Assert.That(BitmapFrame.Create(new MemoryStream(image)).PixelWidth).IsEqualTo(16); // ClipMate's own encoding
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    /// <summary>
    /// The stored Format names of the clip whose text is the screenshot's filename, or null until it is captured.
    /// </summary>
    private List<string>? ReadStoredFormatNamesOfClip()
    {
        if (!File.Exists(appFixture.DatabasePath))
            return null;

        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT d.FormatName FROM ClipData d WHERE d.ClipId = (SELECT ClipId FROM BlobTxt WHERE Data = $text LIMIT 1)";
        command.Parameters.AddWithValue("$text", _fileName);

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));

        return names.Count > 0 ? names : null;
    }

    /// <summary>
    /// A small opaque PNG, so the bytes survive the clipboard's bitmap conversion unchanged when read back as PNG.
    /// </summary>
    private static byte[] CreatePng()
    {
        var pixels = new byte[16 * 16 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0x20;     // B
            pixels[i + 1] = 0x80; // G
            pixels[i + 2] = 0xE0; // R
            pixels[i + 3] = 0xFF; // A
        }

        var bitmap = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
