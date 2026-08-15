using ClipMate.Core.Services;

namespace ClipMate.Data.Services;

/// <inheritdoc cref="IAutoAppendService" />
public class AutoAppendService : IAutoAppendService
{
    public bool IsActive { get; private set; }

    public Guid? GrowingClipId { get; private set; }

    public string? DatabaseKey { get; private set; }

    public void Activate()
    {
        IsActive = true;
        GrowingClipId = null;
        DatabaseKey = null;
    }

    public void Deactivate()
    {
        IsActive = false;
        GrowingClipId = null;
        DatabaseKey = null;
    }

    public void SetGrowingClip(Guid clipId, string databaseKey)
    {
        GrowingClipId = clipId;
        DatabaseKey = databaseKey;
    }
}
