namespace ClipMate.Core.Models;

/// <summary>
/// The clip is a Duplicate of the newest clip in its collection, so it wasn't added.
/// </summary>
/// <param name="Existing">The clip already there.</param>
public sealed record DuplicateFound(Clip Existing) : AddResult;
