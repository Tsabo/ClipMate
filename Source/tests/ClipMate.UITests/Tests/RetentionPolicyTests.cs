using ClipMate.UITests.Fixtures;
using TUnit.Core;

namespace ClipMate.UITests.Tests;

/// <summary>
/// UI tests for retention policy enforcement and visual indicators.
/// Tests RetentionEnforcementService behavior, PurgePolicy modes, and retention cascade.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public class RetentionPolicyTests(AppFixture appFixture)
{
    private readonly AppFixture _appFixture = appFixture;

    [Before(Test)]
    public async Task Setup()
    {
        await _appFixture.StartAppAsync();
    }

    /// <summary>
    /// Verifies that KeepLast policy moves excess clips to Overflow.
    /// </summary>
    [Test]
    [Skip("Requires database setup with retention limits and clip creation")]
    public async Task KeepLast_ExceedsLimit_MovesToOverflow()
    {
        // TODO: 
        // 1. Create collection with RetentionLimit=5, PurgePolicy=KeepLast
        // 2. Add 10 clips
        // 3. Trigger retention enforcement
        // 4. Verify 5 clips in collection, 5 in Overflow
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that PurgeByAge policy soft-deletes expired clips.
    /// </summary>
    [Test]
    [Skip("Requires database setup and time manipulation")]
    public async Task PurgeByAge_ExceedsAge_SoftDeletes()
    {
        // TODO:
        // 1. Create collection with MaxAgeDays=7, PurgePolicy=PurgeByAge
        // 2. Add clips with old timestamps
        // 3. Trigger retention enforcement
        // 4. Verify old clips moved to Trashcan (Del=true)
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that PurgePolicy=Never prevents automatic deletion.
    /// </summary>
    [Test]
    [Skip("Requires database setup and retention enforcement")]
    public async Task Never_Policy_PreventsDeletion()
    {
        // TODO:
        // 1. Create collection with PurgePolicy=Never (no limits)
        // 2. Add many clips
        // 3. Trigger retention enforcement
        // 4. Verify all clips remain in collection
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies retention cascade: Collection → Overflow → Trashcan.
    /// </summary>
    [Test]
    [Skip("Requires complex database setup")]
    public async Task RetentionCascade_FullFlow()
    {
        // TODO:
        // 1. Set InBox limit=5
        // 2. Set Overflow limit=3
        // 3. Add 10 clips to InBox
        // 4. Trigger retention enforcement twice
        // 5. Verify: 5 in InBox, 3 in Overflow, 2 in Trashcan
        await Task.CompletedTask;
    }
}
