using ClipMate.Core.Models;

namespace ClipMate.Data.Services;

/// <summary>
/// One step of a Power Paste sequence: a clip, or one piece of an exploded clip. A step that is not ready still has to
/// have its content loaded, and to be exploded when the Explode setting is on, before it can be offered.
/// </summary>
internal sealed record PowerPasteStep(Clip Clip, bool IsExplodePiece, bool IsReady = true);
