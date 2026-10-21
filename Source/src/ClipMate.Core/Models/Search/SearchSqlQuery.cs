namespace ClipMate.Core.Models.Search;

/// <summary>
/// A search SQL statement together with the parameter values it references.
/// Filter values are never embedded in <see cref="Sql" />; each one is referenced as <c>@Name</c>
/// and supplied through <see cref="Parameters" />, so untrusted values cannot alter the query.
/// </summary>
/// <param name="Sql">The SQL text containing <c>@Name</c> parameter references.</param>
/// <param name="Parameters">Parameter values keyed by name (without the <c>@</c> prefix).</param>
public sealed record SearchSqlQuery(string Sql, IReadOnlyDictionary<string, object?> Parameters);
