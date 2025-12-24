using System.Data;
using Dapper;

namespace ClipMate.Data.Dapper;

/// <summary>
/// Type handler for SQLite DateTimeOffset string conversion.
/// SQLite stores DateTimeOffset as ISO 8601 strings.
/// </summary>
internal class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value)
    {
        return value switch
        {
            string s => DateTimeOffset.Parse(s),
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            var _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to DateTimeOffset"),
        };
    }

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) => parameter.Value = value.ToString("o"); // ISO 8601
}
