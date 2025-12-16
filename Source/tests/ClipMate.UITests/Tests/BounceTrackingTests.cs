using ClipMate.UITests.Fixtures;
using TUnit.Core;

namespace ClipMate.UITests.Tests;

/// <summary>
/// UI tests for bounce tracking functionality.
/// Tests clipboard capture behavior when collections have AcceptNewClips=false.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public class BounceTrackingTests(AppFixture appFixture)
{
    private readonly AppFixture _appFixture = appFixture;

    [Before(Test)]
    public async Task Setup()
    {
        await _appFixture.StartAppAsync();
    }

    /// <summary>
    /// Verifies clips bounce to first accepting collection when active collection rejects.
    /// </summary>
    [Test]
    [Skip("Requires clipboard simulation and database verification")]
    public async Task BounceTracking_AcceptNewClipsFalse_BouncesToFirstAccepting()
    {
        // TODO:
        // 1. Set Work collection as active
        // 2. Set Work.AcceptNewClips=false
        // 3. Ensure InBox.AcceptNewClips=true (lower SortKey)
        // 4. Simulate clipboard capture (copy text)
        // 5. Verify clip created in InBox, not in Work
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies clip is ignored (with sound) when no accepting collection exists.
    /// </summary>
    [Test]
    [Skip("Requires clipboard simulation and all collections rejecting")]
    public async Task BounceTracking_NoAcceptingCollection_IgnoresClip()
    {
        // TODO:
        // 1. Set all collections to AcceptNewClips=false
        // 2. Simulate clipboard capture
        // 3. Verify no clip created in database
        // 4. Verify sound notification played (if possible)
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies bounce respects SortKey ordering.
    /// </summary>
    [Test]
    [Skip("Requires multiple accepting collections with different SortKeys")]
    public async Task BounceTracking_RespectsSortKeyOrder()
    {
        // TODO:
        // 1. Set Work.AcceptNewClips=false (active collection)
        // 2. Create Collection1 (SortKey=10, AcceptNewClips=true)
        // 3. Create Collection2 (SortKey=5, AcceptNewClips=true)
        // 4. Simulate clipboard capture
        // 5. Verify clip goes to Collection2 (lower SortKey = first)
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies virtual and read-only collections are skipped in bounce search.
    /// </summary>
    [Test]
    [Skip("Requires virtual/read-only collection setup")]
    public async Task BounceTracking_SkipsVirtualAndReadOnly()
    {
        // TODO:
        // 1. Create virtual collection (IsVirtual=true, AcceptNewClips=true, SortKey=1)
        // 2. Create read-only collection (ReadOnly=true, AcceptNewClips=true, SortKey=2)
        // 3. Create normal collection (AcceptNewClips=true, SortKey=3)
        // 4. Set active collection to AcceptNewClips=false
        // 5. Simulate clipboard capture
        // 6. Verify clip goes to normal collection (SortKey=3), not virtual/read-only
        await Task.CompletedTask;
    }
}
