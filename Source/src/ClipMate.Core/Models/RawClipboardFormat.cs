namespace ClipMate.Core.Models;

/// <summary>
/// One clipboard Format exactly as the source offered it: its name, its format code and the content of its global
/// memory block. The code of a registered format is only valid in the Windows session that captured it; the name
/// identifies it.
/// </summary>
/// <param name="Name">The format name, e.g. "CF_LOCALE" or "Rich Text Format".</param>
/// <param name="Code">The clipboard format code when it was captured.</param>
/// <param name="Data">The format's bytes.</param>
public sealed record RawClipboardFormat(string Name, uint Code, byte[] Data);
