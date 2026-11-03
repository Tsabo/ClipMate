namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Service for enforcing retention rules on collections.
/// Handles MaxClips, MaxBytes, MaxAge retention policies,
/// Overflow collection behavior, and ReadOnly collection bypass.
/// </summary>
public interface IRetentionEnforcementService
{
    /// <summary>
    /// Enforces retention rules across all collections in the database.
    /// Typically called by scheduled maintenance service.
    /// </summary>
    /// <param name="databaseKey">Database identifier.</param>
    /// <returns>Total number of clips moved/deleted.</returns>
    Task<int> EnforceAllCollectionsAsync(string databaseKey);
}
