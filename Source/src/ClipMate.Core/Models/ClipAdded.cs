namespace ClipMate.Core.Models;

/// <summary>
/// The clip was added.
/// </summary>
/// <param name="Clip">The clip as saved.</param>
public sealed record ClipAdded(Clip Clip) : AddResult;
