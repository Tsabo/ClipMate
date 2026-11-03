using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Records every query EF Core reads with, so a test can look at the SQL a repository really sends.
/// </summary>
internal sealed class SqlCommandRecorder : DbCommandInterceptor
{
    private readonly List<RecordedSqlCommand> _commands = [];

    public IReadOnlyList<RecordedSqlCommand> Commands => _commands;

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    private void Record(DbCommand command) =>
        _commands.Add(new RecordedSqlCommand(command.CommandText,
            command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, (object?)p.Value)).ToList()));
}
