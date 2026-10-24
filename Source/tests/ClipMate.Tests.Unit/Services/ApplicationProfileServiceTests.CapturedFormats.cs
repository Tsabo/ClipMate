using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for GetCapturedFormatsAsync in <see cref="IApplicationProfileService" />: which formats of a capture the
/// Source Application's profile keeps, and how a profile learns formats it hasn't seen.
/// </summary>
[Category("ApplicationProfileService")]
public class ApplicationProfileServiceCapturedFormatsTests : ApplicationProfileServiceTestsBase
{
    [Test]
    public async Task GetCapturedFormatsAsync_ProfilesSwitchedOff_KeepsEveryFormat()
    {
        Service.SetApplicationProfilesEnabled(false);

        var result = await Service.GetCapturedFormatsAsync("notepad.exe", ["CF_UNICODETEXT", "Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["CF_UNICODETEXT", "Acme Private"]);
        A.CallTo(MockStore).MustNotHaveHappened();
    }

    [Test]
    public async Task GetCapturedFormatsAsync_NoProfile_KeepsEveryFormatAndCreatesNoProfile()
    {
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns((ApplicationProfile?)null);

        var result = await Service.GetCapturedFormatsAsync("notepad.exe", ["CF_UNICODETEXT", "CF_LOCALE", "Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["CF_UNICODETEXT", "CF_LOCALE", "Acme Private"]);
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    public async Task GetCapturedFormatsAsync_ProfileSwitchedOff_KeepsNothing()
    {
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        profile.Enabled = false;
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(profile);

        var result = await Service.GetCapturedFormatsAsync("notepad.exe", ["CF_TEXT", "Acme Private"]);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetCapturedFormatsAsync_FormatsTheProfileKnows_FollowsItsSettingsAndSavesNothing()
    {
        // Notepad: TEXT on (stored without the CF_ prefix), CF_UNICODETEXT and LOCALE off
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(ApplicationProfileTestFixtures.GetNotepadProfile());

        var result = await Service.GetCapturedFormatsAsync("notepad.exe", ["CF_TEXT", "CF_UNICODETEXT", "CF_LOCALE"]);

        await Assert.That(result!).IsEquivalentTo(["CF_TEXT"]);
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    public async Task GetCapturedFormatsAsync_NewFormats_AreLearnedWithTheirDefaultAndSaved()
    {
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(profile);
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(profile, A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);

        var result = await Service.GetCapturedFormatsAsync("notepad.exe", ["HTML Format", "Rich Text Format", "CF_DIB", "CF_HDROP", "Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["HTML Format", "Rich Text Format", "CF_DIB", "CF_HDROP"]);
        await Assert.That(profile.Formats["HTML Format"]).IsTrue();
        await Assert.That(profile.Formats["Rich Text Format"]).IsTrue();
        await Assert.That(profile.Formats["CF_DIB"]).IsTrue();
        await Assert.That(profile.Formats["CF_HDROP"]).IsTrue();
        await Assert.That(profile.Formats["Acme Private"]).IsFalse();
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(profile, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task GetCapturedFormatsAsync_LearnedFormatSwitchedOn_IsKept()
    {
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        profile.Formats["Acme Private"] = true;
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).Returns(profile);

        var result = await Service.GetCapturedFormatsAsync("notepad.exe", ["Acme Private"]);

        await Assert.That(result!).IsEquivalentTo(["Acme Private"]);
    }

    [Test]
    public async Task GetCapturedFormatsAsync_ImageWithAnOldBitmapSetting_FollowsItAndLearnsNothing()
    {
        // Chrome: BITMAP and DIB off, stored without the CF_ prefix
        A.CallTo(() => MockStore.GetProfileAsync("CHROME", A<CancellationToken>.Ignored)).Returns(ApplicationProfileTestFixtures.GetChromeProfile());

        var result = await Service.GetCapturedFormatsAsync("chrome.exe", ["CF_DIBV5"]);

        await Assert.That(result!).IsEmpty();
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }
}
