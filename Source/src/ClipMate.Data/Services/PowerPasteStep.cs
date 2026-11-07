using ClipMate.Core.Models;

namespace ClipMate.Data.Services;

/// <summary>
/// One step of a Power Paste sequence: a clip, or one Fragment of an exploded clip. A step that is not ready still has
/// to have its content loaded, and to be exploded when the Explode setting is on, before it can be offered. A Fragment
/// knows where it lies in its clip's text (<paramref name="FragmentStart" />, <paramref name="FragmentLength" />), so it
/// can be shown there.
/// </summary>
internal sealed record PowerPasteStep(Clip Clip, bool IsExplodePiece, bool IsReady = true, int FragmentStart = 0, int FragmentLength = 0);
