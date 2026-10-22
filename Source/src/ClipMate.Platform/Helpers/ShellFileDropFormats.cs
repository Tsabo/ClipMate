using System.Buffers.Binary;
using System.IO;
using Windows.Win32;
using Windows.Win32.UI.Shell.Common;

namespace ClipMate.Platform.Helpers;

/// <summary>
/// Builds the shell formats that Explorer puts on the clipboard alongside CF_HDROP, so a restored file list
/// pastes the way the original copy or cut did.
/// </summary>
public static class ShellFileDropFormats
{
    /// <summary>
    /// Builds a "Shell IDList Array" (CIDA) for the paths: the desktop as parent folder, followed by each
    /// file's absolute item ID list. Returns null if any path no longer resolves to a shell item, because a
    /// partial list would paste fewer files than the CF_HDROP alongside it.
    /// </summary>
    public static unsafe MemoryStream? CreateShellIdListArray(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count == 0)
            return null;

        var itemIdLists = new List<byte[]>(filePaths.Count);
        foreach (var item in filePaths)
        {
            ITEMIDLIST* pidl = null;
            try
            {
                if (PInvoke.SHParseDisplayName(item, null, out pidl, 0, out _).Failed || pidl == null)
                    return null;

                itemIdLists.Add(new ReadOnlySpan<byte>(pidl, (int)PInvoke.ILGetSize(pidl)).ToArray());
            }
            finally
            {
                if (pidl != null)
                    PInvoke.ILFree(pidl);
            }
        }

        // CIDA: UINT cidl; UINT aoffset[cidl + 1]; then the parent ID list and the item ID lists.
        // The desktop's ID list is just its 2-byte terminator.
        const int desktopIdListSize = 2;
        var headerSize = sizeof(uint) * (itemIdLists.Count + 2);
        var buffer = new byte[headerSize + desktopIdListSize + itemIdLists.Sum(p => p.Length)];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)itemIdLists.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(sizeof(uint)), (uint)headerSize);

        var offset = headerSize + desktopIdListSize;
        for (var i = 0; i < itemIdLists.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(sizeof(uint) * (i + 2)), (uint)offset);
            itemIdLists[i].CopyTo(buffer, offset);
            offset += itemIdLists[i].Length;
        }

        return new MemoryStream(buffer);
    }

    /// <summary>
    /// Encodes a "Preferred DropEffect" value as the DWORD the shell expects.
    /// </summary>
    public static MemoryStream CreatePreferredDropEffect(int dropEffect) => new(BitConverter.GetBytes(dropEffect));

    /// <summary>
    /// Reads a "Preferred DropEffect" clipboard value, or null if it is missing or malformed.
    /// </summary>
    public static int? ReadPreferredDropEffect(object? data) =>
        data is MemoryStream { Length: >= sizeof(int) } stream
            ? BitConverter.ToInt32(stream.ToArray(), 0)
            : null;
}
