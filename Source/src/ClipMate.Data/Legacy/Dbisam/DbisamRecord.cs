using System.Buffers.Binary;

namespace ClipMate.Data.Legacy.Dbisam;

/// <summary>
/// A single active record read from a <see cref="DbisamTable" />.
/// Getters return null (or false) for NULL values and for fields the table does not have,
/// so older ClipMate 7 schemas that lack newer columns still import.
/// </summary>
public sealed class DbisamRecord
{
    private readonly byte[] _data;
    private readonly DbisamTable _table;

    internal DbisamRecord(DbisamTable table, byte[] data)
    {
        _table = table;
        _data = data;
    }

    public int? GetInt32(string fieldName) =>
        TryGetValue(fieldName, DbisamFieldType.Integer, out var value)
            ? BinaryPrimitives.ReadInt32LittleEndian(value)
            : null;

    public bool GetBoolean(string fieldName) =>
        TryGetValue(fieldName, DbisamFieldType.Boolean, out var value) &&
        BinaryPrimitives.ReadUInt16LittleEndian(value) != 0;

    public string? GetString(string fieldName)
    {
        if (!TryGetValue(fieldName, DbisamFieldType.String, out var value))
            return null;

        var end = value.IndexOf((byte)0);
        if (end >= 0)
            value = value[..end];

        return DbisamTable.Ansi.GetString(value);
    }

    public Guid? GetGuid(string fieldName) =>
        Guid.TryParse(GetString(fieldName), out var guid)
            ? guid
            : null;

    /// <summary>
    /// Reads a timestamp. DBISAM stores milliseconds since 0001-01-01 plus one day (Delphi TTimeStamp).
    /// The value is the local wall-clock time of the machine that wrote it.
    /// </summary>
    public DateTime? GetDateTime(string fieldName)
    {
        if (!TryGetValue(fieldName, DbisamFieldType.Timestamp, out var value))
            return null;

        var milliseconds = BinaryPrimitives.ReadDoubleLittleEndian(value) - TimeSpan.FromDays(1).TotalMilliseconds;
        if (double.IsNaN(milliseconds) || milliseconds <= 0 || milliseconds >= (DateTime.MaxValue - DateTime.MinValue).TotalMilliseconds)
            return null;

        // Whole milliseconds; multiplying the double directly would lose precision at this magnitude
        return new DateTime((long)Math.Round(milliseconds) * TimeSpan.TicksPerMillisecond, DateTimeKind.Local);
    }

    /// <summary>
    /// Reads a BLOB or memo field. Returns an empty array for NULL.
    /// </summary>
    public byte[] GetBlob(string fieldName) =>
        TryGetValue(fieldName, DbisamFieldType.Blob, out var value)
            ? _table.ReadBlob(BinaryPrimitives.ReadInt32LittleEndian(value))
            : [];

    private bool TryGetValue(string fieldName, DbisamFieldType expectedType, out ReadOnlySpan<byte> value)
    {
        value = default;

        var field = _table.FindField(fieldName);
        if (field is null)
            return false;

        if (field.Type != expectedType)
            throw new DbisamFormatException($"Field '{fieldName}' in table '{_table.Name}' is {field.Type}, not {expectedType}.");

        if (_data[field.Offset] == 0)
            return false; // NULL

        value = _data.AsSpan(field.Offset + 1, field.Size);
        return true;
    }
}
