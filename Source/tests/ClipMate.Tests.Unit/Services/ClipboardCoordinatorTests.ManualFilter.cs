using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Manual Filter puts the Current Clip back with only its plain text or image, as ClipMate 7's does
/// (<c>act_FilterThisClipExecute</c>), rather than filtering whatever is on the clipboard.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    [Test]
    public async Task ManualFilter_StoredCurrentClip_IsWrittenAgainFiltered()
    {
        // Arrange
        var writer = A.Fake<ICurrentClipWriter>();
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Html, TextContent = "Hello" };
        A.CallTo(() => writer.CurrentClip).Returns(new CurrentClip(clip, "db"));
        var messenger = new StrongReferenceMessenger();
        CreateCoordinator(writer, messenger);

        // Act
        messenger.Send(new ManualFilterRequestedEvent());

        // Assert
        await WaitForAsync(() => Fake.GetCalls(writer).Any(p => p.Method.Name == nameof(ICurrentClipWriter.MakeCurrentAsync)));
        A.CallTo(() => writer.MakeCurrentAsync("db", clip.Id, new WriteOptions(FilterMode.Forced), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ManualFilter_CurrentClipNotStored_IsWrittenAgainFilteredAsItIs()
    {
        // Arrange: a Source URL placed on the clipboard
        var writer = A.Fake<ICurrentClipWriter>();
        var content = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "https://example.com" };
        A.CallTo(() => writer.CurrentClip).Returns(new CurrentClip(content, null));
        var messenger = new StrongReferenceMessenger();
        CreateCoordinator(writer, messenger);

        // Act
        messenger.Send(new ManualFilterRequestedEvent());

        // Assert
        await WaitForAsync(() => Fake.GetCalls(writer).Any(p => p.Method.Name == nameof(ICurrentClipWriter.PutOnClipboardAsync)));
        A.CallTo(() => writer.PutOnClipboardAsync(content, new WriteOptions(FilterMode.Forced), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ManualFilter_NoCurrentClip_WritesNothing()
    {
        // Arrange: ClipMate just started, nothing captured or picked yet
        var writer = A.Fake<ICurrentClipWriter>();
        A.CallTo(() => writer.CurrentClip).Returns(null);
        var messenger = new StrongReferenceMessenger();
        CreateCoordinator(writer, messenger);

        // Act
        messenger.Send(new ManualFilterRequestedEvent());
        await Task.Delay(100);

        // Assert
        A.CallTo(() => writer.MakeCurrentAsync(A<string>._, A<Guid>._, A<WriteOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => writer.PutOnClipboardAsync(A<Clip>._, A<WriteOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private ClipboardCoordinator CreateCoordinator(ICurrentClipWriter writer, IMessenger messenger) =>
        new(CreateMockClipboardService(out _), CreateMockConfigurationService(), writer, A.Fake<IDatabaseContextFactory>(),
            A.Fake<IAutoAppendService>(), CreateMockServiceProvider(), messenger, CreateLogger<ClipboardCoordinator>());

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }
}
