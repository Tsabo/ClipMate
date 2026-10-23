using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using IDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using ClipMate.Core.Models;

namespace ClipMate.Platform.Helpers;

/// <summary>
/// Converts between a data object's Formats and the raw global-memory data the clipboard holds, so a clip can be
/// offered by delayed rendering and rendered as often as it is requested.
/// </summary>
public static class DataObjectFormats
{
    private const int _bitmapInfoHeaderSize = 40;

    /// <summary>
    /// Reads every format the data object can render into global memory. Formats it offers only as a GDI handle or a
    /// stream object, and formats it fails to render, are left out.
    /// </summary>
    public static IReadOnlyList<ClipboardFormatData> Read(IDataObject dataObject)
    {
        ArgumentNullException.ThrowIfNull(dataObject);

        var result = new List<ClipboardFormatData>();
        var enumerator = dataObject.EnumFormatEtc(DATADIR.DATADIR_GET);
        var formats = new FORMATETC[1];
        var fetched = new int[1];
        while (enumerator.Next(1, formats, fetched) == 0 && fetched[0] == 1)
        {
            var format = formats[0];
            var formatId = (uint)(ushort)format.cfFormat;
            if ((format.tymed & TYMED.TYMED_HGLOBAL) == 0 || result.Any(p => p.FormatId == formatId))
                continue;

            format.tymed = TYMED.TYMED_HGLOBAL;
            try
            {
                dataObject.GetData(ref format, out var medium);
                try
                {
                    if (CopyGlobal(medium.unionmember) is { } data)
                        result.Add(new ClipboardFormatData(formatId, data));
                }
                finally
                {
                    ReleaseStgMedium(ref medium);
                }
            }
            catch (COMException)
            {
                // e.g. WPF's own serialized-object formats, which it renders only as a stream
            }
        }

        return result;
    }

    /// <summary>
    /// Copies the data into a new movable global memory block, which the clipboard takes ownership of once passed to
    /// SetClipboardData. Returns <see cref="IntPtr.Zero" /> if the block cannot be allocated.
    /// </summary>
    public static unsafe IntPtr AllocGlobal(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // A zero-length block is valid but some readers treat a missing terminator as an error; never allocate 0 bytes
        var hMem = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)Math.Max(data.Length, 1));
        if (hMem.IsNull)
            return IntPtr.Zero;

        var ptr = PInvoke.GlobalLock(hMem);
        if (ptr == null)
        {
            PInvoke.GlobalFree(hMem);

            return IntPtr.Zero;
        }

        try
        {
            data.CopyTo(new Span<byte>(ptr, data.Length));
        }
        finally
        {
            PInvoke.GlobalUnlock(hMem);
        }

        return (IntPtr)hMem.Value;
    }

    /// <summary>
    /// CF_UNICODETEXT data for the text: UTF-16 with a terminating null character.
    /// </summary>
    public static byte[] EncodeUnicodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var data = new byte[(text.Length + 1) * sizeof(char)];
        MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(data);

        return data;
    }

    /// <summary>
    /// CF_DIB data for an encoded image (PNG, JPEG, BMP...): a 32-bit bottom-up BITMAPINFOHEADER bitmap.
    /// </summary>
    public static byte[] EncodeDib(byte[] imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        using var stream = new MemoryStream(imageData);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);

        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var dib = new byte[_bitmapInfoHeaderSize + pixels.Length];
        var header = dib.AsSpan(0, _bitmapInfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header, _bitmapInfoHeaderSize); // biSize
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], width); // biWidth
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], height); // biHeight: positive, rows bottom-up
        BinaryPrimitives.WriteInt16LittleEndian(header[12..], 1); // biPlanes
        BinaryPrimitives.WriteInt16LittleEndian(header[14..], 32); // biBitCount; biCompression BI_RGB (0)
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], pixels.Length); // biSizeImage

        for (var row = 0; row < height; row++)
            pixels.AsSpan(row * stride, stride).CopyTo(dib.AsSpan(_bitmapInfoHeaderSize + (height - 1 - row) * stride));

        return dib;
    }

    private static unsafe byte[]? CopyGlobal(IntPtr handle)
    {
        var hMem = new HGLOBAL((void*)handle);
        var size = (int)PInvoke.GlobalSize(hMem);
        var ptr = PInvoke.GlobalLock(hMem);
        if (ptr == null)
            return null;

        try
        {
            return new ReadOnlySpan<byte>(ptr, size).ToArray();
        }
        finally
        {
            PInvoke.GlobalUnlock(hMem);
        }
    }

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}
