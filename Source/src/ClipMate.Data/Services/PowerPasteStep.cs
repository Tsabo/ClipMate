using ClipMate.Core.Models;

namespace ClipMate.Data.Services;

/// <summary>
/// One step of a Power Paste sequence: a clip, or one piece of an exploded clip.
/// </summary>
internal sealed record PowerPasteStep(Clip Clip, bool IsExplodePiece);
