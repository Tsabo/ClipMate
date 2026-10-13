namespace ClipMate.Data.Legacy.Dbisam;

/// <summary>
/// DBISAM 4 field data types (descriptor offset 0xA4). Only the types used by ClipMate 7 are listed.
/// </summary>
internal enum DbisamFieldType : byte
{
    String = 1,
    Blob = 3,
    Boolean = 4,
    Integer = 6,
    Timestamp = 11,
}

/// <summary>
/// A field (column) definition parsed from a DBISAM table header.
/// </summary>
/// <param name="Name">Column name as stored in the table (upper case for ClipMate tables).</param>
/// <param name="Type">Field data type.</param>
/// <param name="Size">Physical size of the value in bytes, excluding the leading null-flag byte.</param>
/// <param name="Offset">Offset of the null-flag byte within the record; the value follows it.</param>
internal sealed record DbisamField(string Name, DbisamFieldType Type, int Size, int Offset);

/// <summary>
/// Thrown when a file is not a readable DBISAM 4 table, or a table is damaged.
/// </summary>
public sealed class DbisamFormatException(string message) : Exception(message);
