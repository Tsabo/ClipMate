using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Data.Services;

/// <inheritdoc cref="IAppendMode" />
public class AppendMode : IAppendMode
{
    public bool IsActive { get; private set; }

    public Guid? GrowingClipId { get; private set; }

    public string? DatabaseKey { get; private set; }

    public void Start(Guid growingClipId, string databaseKey)
    {
        IsActive = true;
        GrowingClipId = growingClipId;
        DatabaseKey = databaseKey;
    }

    public void Stop()
    {
        IsActive = false;
        GrowingClipId = null;
        DatabaseKey = null;
    }
}
