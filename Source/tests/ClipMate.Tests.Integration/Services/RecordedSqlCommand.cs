namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// One SQL command EF Core sent to the database, with its parameter values.
/// </summary>
internal sealed record RecordedSqlCommand(string Sql, IReadOnlyList<(string Name, object? Value)> Parameters);
