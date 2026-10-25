using System.IO;
using System.Windows;
using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.Tests;

/// <summary>
/// An application that copies its own private format alongside text: ClipMate must store that format as the source
/// offered it and offer it again when the clip is picked, so pasting back into the application keeps its data.
/// <para>Uses the real, shared clipboard and puts its previous content back afterwards.</para>
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.None)]
[NotInParallel(SystemClipboard.ParallelKey)]
public class ClipboardRawFormatRoundTripTests(AppFixture appFixture)
{
    private const string _privateFormat = "ClipMate.UITests.RoundTripFormat";
    private const string _probeText = "probe text";
    private static readonly byte[] _privateData = [1, 2, 3, 4, 5, 250];

    // Unique per run, so a copy left on the clipboard by an earlier run is never mistaken for this one
    private readonly string _text = $"private format {Guid.NewGuid():N}";

    [Test]
    public async Task TextWithAPrivateFormat_CapturedAndPicked_OffersThePrivateFormatAgain()
    {
        var backup = SystemClipboard.Backup();
        try
        {
            TestProgramProfile.Write(appFixture, _privateFormat);

            await appFixture.StartAppAsync();
            var clipList = new ClipListPage(appFixture.Process);

            // Arrange: text plus a format only the source application understands
            using var source = await ForegroundTestWindow.OpenAsync();
            source.Copy(() =>
            {
                var copy = new DataObject();
                copy.SetText(_text);
                copy.SetData(_privateFormat, new MemoryStream(_privateData));
                return copy;
            });

            var stored = await Poll.UntilAsync(ReadStoredFormatNamesOfClip, "ClipMate to capture the copy");
            await Assert.That(stored).Contains("CF_UNICODETEXT");
            await Assert.That(stored).Contains(_privateFormat);

            // Act: replace the clipboard, then pick the clip
            await clipList.WaitForRowAsync(_text);
            var probeAttempt = 0;
            await Poll.UntilAsync(() =>
            {
                if (probeAttempt++ % 10 == 0)
                    SystemClipboard.OnSta(() => Clipboard.SetText(_probeText));

                return clipList.FindRow(_probeText);
            }, "the probe clip in the clip list");

            await clipList.SelectAsync(_text);
            await Poll.UntilAsync(() => SystemClipboard.TryGetRawFormatNames()?.Contains(_privateFormat) == true,
                "the clip to be copied back with its private format");

            // Assert: the private format comes back with the source's bytes, alongside the text
            var (text, data) = SystemClipboard.OnSta(() => (Clipboard.GetText(), (Clipboard.GetData(_privateFormat) as MemoryStream)?.ToArray()));
            await Assert.That(text).IsEqualTo(_text);
            await Assert.That(data).IsNotNull();
            await Assert.That(data!.Take(_privateData.Length).ToArray()).IsEquivalentTo(_privateData);
        }
        finally
        {
            SystemClipboard.Restore(backup);
        }
    }

    /// <summary>
    /// The stored Format names of the clip whose text is this run's text, or null until it is captured.
    /// </summary>
    private List<string>? ReadStoredFormatNamesOfClip()
    {
        if (!File.Exists(appFixture.DatabasePath))
            return null;

        using var connection = new SqliteConnection($"Data Source={appFixture.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT d.FormatName FROM ClipData d WHERE d.ClipId = (SELECT ClipId FROM BlobTxt WHERE Data = $text LIMIT 1)";
        command.Parameters.AddWithValue("$text", _text);

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));

        return names.Count > 0 ? names : null;
    }
}
