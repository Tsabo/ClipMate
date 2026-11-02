namespace ClipMate.Core.Models.Capture;

/// <summary>
/// Where a new capture goes.
/// </summary>
/// <param name="DatabaseKey">The Database.</param>
/// <param name="Collection">The collection, after the bounce.</param>
/// <param name="FolderId">The folder, if any.</param>
public sealed record CaptureTarget(string DatabaseKey, Collection Collection, Guid? FolderId);
