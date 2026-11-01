namespace ClipMate.Data.Services;

/// <summary>
/// The placeholders a Smart or SQL-based collection's stored SQL may use.
/// </summary>
internal static class VirtualCollectionSql
{
    /// <summary>
    /// Replaces SQL query placeholders with actual values.
    /// Supported placeholders:
    /// - #DATE# - Today's date at midnight (YYYY-MM-DD HH:MM:SS)
    /// - #DATEMINUSLIMIT# - Date minus retention limit days
    /// - #DATELASTIMPORT# - Date of last import (placeholder for future feature, defaults to 30 days ago)
    /// - #DATELASTEXPORT# - Date of last export (placeholder for future feature, defaults to 30 days ago)
    /// </summary>
    public static string ReplacePlaceholders(string sqlQuery, int retentionLimit)
    {
        var now = DateTime.Now;
        var today = now.Date; // Midnight today
        var dateMinusLimit = today.AddDays(-retentionLimit);

        // Format dates for SQLite (ISO 8601 format: YYYY-MM-DD HH:MM:SS)
        var todayStr = today.ToString("yyyy-MM-dd HH:mm:ss");
        var dateMinusLimitStr = dateMinusLimit.ToString("yyyy-MM-dd HH:mm:ss");

        // For now, use fixed defaults for import/export dates (feature not yet implemented)
        var lastImportStr = today.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss");
        var lastExportStr = today.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss");

        // Replace all placeholders
        var result = sqlQuery
            .Replace("#DATE#", todayStr, StringComparison.OrdinalIgnoreCase)
            .Replace("#DATEMINUSLIMIT#", dateMinusLimitStr, StringComparison.OrdinalIgnoreCase)
            .Replace("#DATELASTIMPORT#", lastImportStr, StringComparison.OrdinalIgnoreCase)
            .Replace("#DATELASTEXPORT#", lastExportStr, StringComparison.OrdinalIgnoreCase);

        return result;
    }
}
