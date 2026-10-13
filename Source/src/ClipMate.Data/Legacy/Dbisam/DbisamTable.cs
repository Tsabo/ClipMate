using System.Buffers.Binary;
using System.Text;

namespace ClipMate.Data.Legacy.Dbisam;

/// <summary>
/// Read-only reader for Elevatesoft DBISAM 4 tables (the database engine used by ClipMate 7.x).
/// A table consists of <c>name.dat</c> (fixed-length records) and, when it has BLOB/memo fields,
/// <c>name.blb</c> (chained blocks). Indexes (<c>.idx</c>) are not needed for a full sequential read.
/// </summary>
/// <remarks>
/// Layout (reverse-engineered from ClipMate 7 databases):
/// <list type="bullet">
/// <item>.dat header: signature at 0x08 (16 bytes), active record count (int32) at 0x29, record size (uint16)
/// at 0x2D, field count (uint16) at 0x2F, BLOB block size (uint16) at 0x33. Field descriptors start at 0x200,
/// 768 bytes each: name length at 0x02, name at 0x03, type at 0xA4, size (uint16) at 0xA9, offset (uint16) at 0xAC.</item>
/// <item>Records follow the descriptors. Byte 0 of a record is 0 for an active row (non-zero = deleted).
/// Each value is preceded by a null-flag byte (1 = has value).</item>
/// <item>Strings are NUL-padded Windows-1252, integers int32, booleans 16-bit, timestamps a double holding
/// milliseconds since 0001-01-01 plus one day. A BLOB value is the int32 number of its first block.</item>
/// <item>.blb: logical block n starts at 512 + (n - 1) * blockSize. Block header: previous (int32), next (int32),
/// bytes used (uint16), row number (int32), total BLOB size (int32, first block only); data follows at 18.
/// Chains may carry stale trailing blocks, so reading stops once the total size has been collected.</item>
/// </list>
/// The files are opened with <see cref="FileShare.ReadWrite" /> so a running ClipMate 7 does not block reading.
/// </remarks>
public sealed class DbisamTable : IDisposable
{
    private const int _signatureOffset = 0x08;
    private const int _recordCountOffset = 0x29;
    private const int _recordSizeOffset = 0x2D;
    private const int _fieldCountOffset = 0x2F;
    private const int _blobBlockSizeOffset = 0x33;
    private const int _fieldDescriptorsOffset = 0x200;
    private const int _fieldDescriptorSize = 768;
    private const int _blobDataOffset = 512;
    private const int _blobBlockHeaderSize = 18;

    private static readonly byte[] _signature =
        [0x06, 0x8A, 0xBE, 0x8E, 0x59, 0x23, 0x64, 0xCB, 0x40, 0x3D, 0x71, 0xD2, 0xE3, 0xBC, 0x64, 0xD0];

    private readonly FileStream? _blobStream;
    private readonly FileStream _dataStream;
    private readonly Dictionary<string, DbisamField> _fieldsByName;
    private readonly int _recordSize;
    private readonly long _recordsOffset;
    private readonly int _blobBlockSize;

    private DbisamTable(string name, FileStream dataStream, FileStream? blobStream)
    {
        Name = name;
        _dataStream = dataStream;
        _blobStream = blobStream;

        var header = ReadExactly(_dataStream, 0, _fieldDescriptorsOffset, "table header");
        if (!header.AsSpan(_signatureOffset, _signature.Length).SequenceEqual(_signature))
            throw new DbisamFormatException($"Table '{name}' is not a DBISAM 4 table (unrecognized header signature).");

        ActiveRecordCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(_recordCountOffset));
        _recordSize = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(_recordSizeOffset));
        var fieldCount = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(_fieldCountOffset));
        _blobBlockSize = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(_blobBlockSizeOffset));

        if (_recordSize == 0 || fieldCount == 0)
            throw new DbisamFormatException($"Table '{name}' has an invalid header (record size {_recordSize}, {fieldCount} fields).");

        _recordsOffset = _fieldDescriptorsOffset + (long)fieldCount * _fieldDescriptorSize;
        var descriptors = ReadExactly(_dataStream, _fieldDescriptorsOffset, fieldCount * _fieldDescriptorSize, "field descriptors");

        var fields = new List<DbisamField>(fieldCount);
        for (var i = 0; i < fieldCount; i++)
        {
            var descriptor = descriptors.AsSpan(i * _fieldDescriptorSize, _fieldDescriptorSize);
            var nameLength = descriptor[2];
            var fieldName = Encoding.ASCII.GetString(descriptor.Slice(3, nameLength));
            var size = BinaryPrimitives.ReadUInt16LittleEndian(descriptor[0xA9..]);
            var offset = BinaryPrimitives.ReadUInt16LittleEndian(descriptor[0xAC..]);

            if (offset + 1 + size > _recordSize)
                throw new DbisamFormatException($"Field '{fieldName}' in table '{name}' lies outside the record.");

            fields.Add(new DbisamField(fieldName, (DbisamFieldType)descriptor[0xA4], size, offset));
        }

        Fields = fields;
        _fieldsByName = fields.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Windows-1252, the code page ClipMate 7 used for string and memo fields.
    /// </summary>
    internal static Encoding Ansi { get; } = CreateAnsiEncoding();

    /// <summary>
    /// Table name (file name without extension).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of active (non-deleted) records according to the table header.
    /// </summary>
    public int ActiveRecordCount { get; }

    internal IReadOnlyList<DbisamField> Fields { get; }

    public void Dispose()
    {
        _dataStream.Dispose();
        _blobStream?.Dispose();
    }

    /// <summary>
    /// Returns true if <c>tableName.dat</c> exists in <paramref name="folder" />.
    /// </summary>
    public static bool Exists(string folder, string tableName) => File.Exists(Path.Combine(folder, tableName + ".dat"));

    /// <summary>
    /// Opens a DBISAM table for reading.
    /// </summary>
    /// <param name="folder">Directory containing the table files.</param>
    /// <param name="tableName">Table name without extension (case-insensitive on Windows).</param>
    /// <exception cref="FileNotFoundException">The .dat file does not exist.</exception>
    /// <exception cref="DbisamFormatException">The file is not a readable DBISAM 4 table.</exception>
    public static DbisamTable Open(string folder, string tableName)
    {
        var dataPath = Path.Combine(folder, tableName + ".dat");
        if (!File.Exists(dataPath))
            throw new FileNotFoundException($"DBISAM table '{tableName}' not found.", dataPath);

        var dataStream = OpenShared(dataPath);
        FileStream? blobStream = null;
        try
        {
            var blobPath = Path.Combine(folder, tableName + ".blb");
            if (File.Exists(blobPath))
                blobStream = OpenShared(blobPath);

            return new DbisamTable(tableName, dataStream, blobStream);
        }
        catch
        {
            dataStream.Dispose();
            blobStream?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads all active records sequentially.
    /// </summary>
    public IEnumerable<DbisamRecord> ReadRecords()
    {
        var slotCount = (_dataStream.Length - _recordsOffset) / _recordSize;
        _dataStream.Position = _recordsOffset;

        for (long i = 0; i < slotCount; i++)
        {
            var buffer = new byte[_recordSize];
            _dataStream.ReadExactly(buffer);

            if (buffer[0] != 0)
                continue; // Deleted / free slot

            yield return new DbisamRecord(this, buffer);
        }
    }

    internal DbisamField? FindField(string name) => _fieldsByName.GetValueOrDefault(name);

    /// <summary>
    /// Reads a BLOB that starts at the given logical block.
    /// </summary>
    internal byte[] ReadBlob(int firstBlock)
    {
        if (firstBlock <= 0)
            return [];

        if (_blobStream is null)
            throw new DbisamFormatException($"Table '{Name}' has a BLOB field but no .blb file.");

        if (_blobBlockSize <= _blobBlockHeaderSize)
            throw new DbisamFormatException($"Table '{Name}' has an invalid BLOB block size ({_blobBlockSize}).");

        var maxBlocks = (_blobStream.Length - _blobDataOffset) / _blobBlockSize;
        var block = new byte[_blobBlockSize];
        byte[]? result = null;
        var collected = 0;
        var current = firstBlock;

        for (var visited = 0; visited <= maxBlocks; visited++)
        {
            if (current < 1 || current > maxBlocks)
                throw new DbisamFormatException($"BLOB chain in table '{Name}' points outside the file (block {current}).");

            _blobStream.Position = _blobDataOffset + (long)(current - 1) * _blobBlockSize;
            _blobStream.ReadExactly(block);

            var next = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(4));
            int used = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(8));

            if (result is null)
            {
                var total = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(14));
                if (total < 0)
                    throw new DbisamFormatException($"BLOB in table '{Name}' has an invalid size ({total}).");

                result = new byte[total];
            }

            var count = Math.Min(Math.Min(used, _blobBlockSize - _blobBlockHeaderSize), result.Length - collected);
            block.AsSpan(_blobBlockHeaderSize, count).CopyTo(result.AsSpan(collected));
            collected += count;

            if (collected >= result.Length)
                return result;

            if (next == 0)
                break;

            current = next;
        }

        throw new DbisamFormatException($"BLOB chain in table '{Name}' starting at block {firstBlock} is truncated.");
    }

    private static Encoding CreateAnsiEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024);

    private static byte[] ReadExactly(FileStream stream, long offset, int count, string what)
    {
        if (stream.Length < offset + count)
            throw new DbisamFormatException($"File '{stream.Name}' is too short to contain the {what}.");

        var buffer = new byte[count];
        stream.Position = offset;
        stream.ReadExactly(buffer);
        return buffer;
    }
}
