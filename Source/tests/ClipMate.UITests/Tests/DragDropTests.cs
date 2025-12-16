using ClipMate.UITests.Fixtures;
using ClipMate.UITests.PageObjects;
using TUnit.Core;

namespace ClipMate.UITests.Tests;

/// <summary>
/// UI tests for drag-drop functionality in collection tree and clip list.
/// Tests collection reordering, clip movement, soft-delete, and restore operations.
/// NOTE: Full implementation requires WPF Pilot drag-drop API learning and test data setup.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public class DragDropTests(AppFixture appFixture)
{
    private readonly AppFixture _appFixture = appFixture;

    [Before(Test)]
    public async Task Setup()
    {
        await _appFixture.StartAppAsync();
    }

    /// <summary>
    /// Verifies collections can be reordered via drag-drop.
    /// Full implementation requires WPF Pilot drag-drop API and manual sort mode.
    /// </summary>
    [Test]
    [Skip("Requires WPF Pilot drag-drop API, test data, and manual sort mode")]
    public async Task DragDrop_CollectionReordering_SwapsSortKeys()
    {
        // TODO: Implement with WPF Pilot drag-drop API
        // Example approach:
        // 1. Get tree element
        // 2. Get source and target collection nodes
        // 3. Use WPF Pilot drag-drop: _appDriver.DragAndDrop(source, target)
        // 4. Verify SortKey values swapped in database
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies clips can be moved between collections via drag-drop.
    /// </summary>
    [Test]
    [Skip("Requires WPF Pilot drag-drop API and clip list page object")]
    public async Task DragDrop_ClipToCollection_UpdatesCollectionId()
    {
        // TODO: Implement with ClipListPage and WPF Pilot drag-drop
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies dragging clip to Trashcan soft-deletes it.
    /// </summary>
    [Test]
    [Skip("Requires WPF Pilot drag-drop API and clip list page object")]
    public async Task DragDrop_ClipToTrashcan_SoftDeletes()
    {
        // TODO:
        // 1. Create test clip in InBox
        // 2. Drag clip to Trashcan using WPF Pilot
        // 3. Verify clip Del=true in database
        // 4. Verify clip appears in Trashcan virtual collection
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies dragging clip from Trashcan to collection restores it.
    /// </summary>
    [Test]
    [Skip("Requires WPF Pilot drag-drop API and clip list page object")]
    public async Task DragDrop_ClipFromTrashcan_Restores()
    {
        // TODO:
        // 1. Create soft-deleted clip (Del=true)
        // 2. Navigate to Trashcan
        // 3. Drag clip to InBox using WPF Pilot
        // 4. Verify clip Del=false, CollectionId updated
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies drag-drop is disabled in alphabetic sort mode.
    /// </summary>
    [Test]
    [Skip("Requires preferences API and drag-drop testing")]
    public async Task DragDrop_AlphabeticMode_DisablesReordering()
    {
        // TODO:
        // 1. Set SortCollectionsAlphabetically=true
        // 2. Attempt to drag-drop collection
        // 3. Verify drop is rejected (SortKey unchanged)
        await Task.CompletedTask;
    }
}

