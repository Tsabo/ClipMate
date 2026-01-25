using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for generating print documents from clip data.
/// </summary>
public interface IPrintService
{
    /// <summary>
    /// Loads clip data for printing.
    /// </summary>
    /// <param name="clipIds">The IDs of clips to load.</param>
    /// <param name="databaseKey">The database key (file path) for accessing clips.</param>
    /// <returns>Clip data ready for printing.</returns>
    Task<List<PrintClipData>> LoadClipDataForPrintingAsync(List<Guid> clipIds, string databaseKey);
}
