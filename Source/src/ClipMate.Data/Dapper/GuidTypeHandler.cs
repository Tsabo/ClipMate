using System.Data;
using Dapper;

namespace ClipMate.Data.Dapper;

/// <summary>
/// Type handler for SQLite GUID string conversion.
/// SQLite stores GUIDs as strings, Dapper needs help converting them.
/// </summary>
internal class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value)
    {
        return value switch
        {
            string s => Guid.Parse(s),
            Guid g => g,
            var _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to Guid"),
        };
    }

    public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value.ToString();
}
