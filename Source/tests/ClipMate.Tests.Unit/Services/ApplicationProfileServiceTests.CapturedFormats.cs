using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for ApplyProfileAsync in <see cref="IApplicationProfileService" />: which formats of a capture the
/// Source Application's profile keeps, and how a profile learns formats it hasn't seen.
/// </summary>
[Category("ApplicationProfileService")]
public class ApplicationProfileServiceCapturedFormatsTests : ApplicationProfileServiceTestsBase
{
    private void GivenProfilesAreSaved() =>
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);

    [Test]
    public async Task ApplyProfileAsync_ProfilesSwitchedOff_KeepsEveryFormat()
    {
        Service.SetApplicationProfilesEnabled(false);

        var result = await Service.ApplyProfileAsync("notepad.exe", ["CF_UNICODETEXT", "Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["CF_UNICODETEXT", "Acme Private"]);
        A.CallTo(MockStore).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyProfileAsync_NoProfile_CreatesOneWithHtmlAndTextOnAndSavesIt()
    {
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns((ApplicationProfile?)null);
        ApplicationProfile? saved = null;
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored))
            .Invokes((ApplicationProfile p, CancellationToken _) => saved = p);

        var result = await Service.ApplyProfileAsync("notepad.exe", ["CF_TEXT", "HTML Format", "CF_DIB", "Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["CF_TEXT", "HTML Format"]);
        await Assert.That(saved!.ApplicationName).IsEqualTo("NOTEPAD");
        await Assert.That(saved.Formats).IsEquivalentTo(new Dictionary<string, bool>
        {
            ["CF_TEXT"] = true, ["HTML Format"] = true, ["CF_DIB"] = false, ["Acme Private"] = false,
        });
    }

    [Test]
    public async Task ApplyProfileAsync_NoProfileAndNoText_TurnsTheImageOn()
    {
        A.CallTo(() => MockStore.GetProfileAsync("SNAGITEDITOR", A<CancellationToken>.Ignored)).Returns((ApplicationProfile?)null);
        GivenProfilesAreSaved();

        var result = await Service.ApplyProfileAsync("SnagitEditor.exe", ["CF_DIB", "{526385E6-2B32-42CC-9689-E0EF8FA0A5D3}"]);

        await Assert.That(result!).IsEquivalentTo(["CF_DIB"]);
    }

    [Test]
    [Arguments("excel.exe", new[] { "CF_TEXT" })]
    [Arguments("WINWORD.EXE", new[] { "CF_TEXT", "Rich Text Format" })]
    [Arguments("firefox.exe", new[] { "CF_TEXT", "HTML Format", "CF_DIB" })]
    [Arguments("explorer.exe", new[] { "CF_TEXT", "HTML Format", "CF_DIB", "CF_HDROP", "FileName" })]
    public async Task ApplyProfileAsync_NoProfileForAProgramClipMate7Knows_UsesItsPreset(string application, string[] expected)
    {
        A.CallTo(() => MockStore.GetProfileAsync(A<string>.Ignored, A<CancellationToken>.Ignored)).Returns((ApplicationProfile?)null);
        GivenProfilesAreSaved();

        var result = await Service.ApplyProfileAsync(application,
            ["CF_TEXT", "Rich Text Format", "HTML Format", "CF_DIB", "CF_HDROP", "FileName", "Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(expected);
    }

    [Test]
    public async Task ApplyProfileAsync_NoProfile_SavesTheNewProfileOnce()
    {
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns((ApplicationProfile?)null);
        GivenProfilesAreSaved();

        await Service.ApplyProfileAsync("notepad.exe", ["CF_TEXT"]);

        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ApplyProfileAsync_ProfileSwitchedOff_KeepsNothing()
    {
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        profile.Enabled = false;
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(profile);

        var result = await Service.ApplyProfileAsync("notepad.exe", ["CF_TEXT", "Acme Private"]);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ApplyProfileAsync_ProfileSwitchedOffButEvenWhenSwitchedOff_FollowsItsFormatSettings()
    {
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        profile.Enabled = false;
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(profile);

        var result = await Service.ApplyProfileAsync("notepad.exe", ["CF_TEXT", "CF_UNICODETEXT"], evenWhenSwitchedOff: true);

        await Assert.That(result!).IsEquivalentTo(["CF_TEXT"]);
    }

    [Test]
    public async Task ApplyProfileAsync_FormatsTheProfileKnows_FollowsItsSettingsAndSavesNothing()
    {
        // Notepad: TEXT on (stored without the CF_ prefix), CF_UNICODETEXT and LOCALE off
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(ApplicationProfileTestFixtures.GetNotepadProfile());

        var result = await Service.ApplyProfileAsync("notepad.exe", ["CF_TEXT", "CF_UNICODETEXT", "CF_LOCALE"]);

        await Assert.That(result!).IsEquivalentTo(["CF_TEXT"]);
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyProfileAsync_NewFormats_TextAndRtfOnOthersOffAndSaved()
    {
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(profile);
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(profile, A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);

        var result = await Service.ApplyProfileAsync("notepad.exe", ["HTML Format", "Rich Text Format", "CF_DIB", "CF_HDROP", "Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["Rich Text Format"]);
        await Assert.That(profile.Formats["HTML Format"]).IsFalse();
        await Assert.That(profile.Formats["Rich Text Format"]).IsTrue();
        await Assert.That(profile.Formats["CF_DIB"]).IsFalse();
        await Assert.That(profile.Formats["CF_HDROP"]).IsFalse();
        await Assert.That(profile.Formats["Acme Private"]).IsFalse();
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(profile, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ApplyProfileAsync_LearnedFormatSwitchedOn_IsKept()
    {
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        profile.Formats["Acme Private"] = true;
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(profile);

        var result = await Service.ApplyProfileAsync("notepad.exe", ["Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["Acme Private"]);
    }

    [Test]
    public async Task ApplyProfileAsync_ImageWithAnOldBitmapSetting_FollowsItAndLearnsNothing()
    {
        // Chrome: BITMAP and DIB off, stored without the CF_ prefix
        A.CallTo(() => MockStore.GetProfileAsync("CHROME", A<CancellationToken>.Ignored)).Returns(ApplicationProfileTestFixtures.GetChromeProfile());

        var result = await Service.ApplyProfileAsync("chrome.exe", ["CF_DIBV5"]);

        await Assert.That(result!).IsEmpty();
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }
}
