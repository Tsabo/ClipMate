using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Manual Filter puts the Current Clip back with only its plain text or image, as ClipMate 7's does
/// (<c>act_FilterThisClipExecute</c>), rather than filtering whatever is on the clipboard.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    [Test]
    public async Task ManualFilter_CurrentClip_IsWrittenAgainFiltered()
    {
        // Arrange
        var writer = A.Fake<ICurrentClipWriter>();
        A.CallTo(() => writer.RewriteCurrentAsync(A<WriteOptions>._, A<CancellationToken>._)).Returns(true);
        var messenger = new StrongReferenceMessenger();
        CreateCoordinator(writer, messenger);

        // Act
        messenger.Send(new ManualFilterRequestedEvent());

        // Assert
        await WaitForAsync(() => Fake.GetCalls(writer).Any(p => p.Method.Name == nameof(ICurrentClipWriter.RewriteCurrentAsync)));
        A.CallTo(() => writer.RewriteCurrentAsync(new WriteOptions(FilterMode.Forced), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ManualFilter_NoCurrentClip_WritesNothing()
    {
        // Arrange: ClipMate just started, nothing captured or picked yet
        var writer = A.Fake<ICurrentClipWriter>();
        A.CallTo(() => writer.RewriteCurrentAsync(A<WriteOptions>._, A<CancellationToken>._)).Returns(false);
        var messenger = new StrongReferenceMessenger();
        CreateCoordinator(writer, messenger);

        // Act
        messenger.Send(new ManualFilterRequestedEvent());
        await WaitForAsync(() => Fake.GetCalls(writer).Any(p => p.Method.Name == nameof(ICurrentClipWriter.RewriteCurrentAsync)));

        // Assert
        A.CallTo(() => writer.MakeCurrentAsync(A<string>._, A<Guid>._, A<WriteOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => writer.PutOnClipboardAsync(A<Clip>._, A<WriteOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private ClipboardCoordinator CreateCoordinator(ICurrentClipWriter writer, IMessenger messenger) =>
        new(CreateMockClipboardService(out _), CreateMockConfigurationService(), writer, A.Fake<IDatabaseContextFactory>(),
            A.Fake<IClipStore>(), A.Fake<IAutoAppendService>(), CreateMockServiceProvider(), messenger, CreateLogger<ClipboardCoordinator>());

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }
}
