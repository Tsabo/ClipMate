namespace ClipMate.Core.Services;

/// <summary>
/// Service for enforcing retention rules on collections.
/// Handles MaxClips, MaxBytes, MaxAge retention policies,
/// Overflow collection behavior, and ReadOnly collection bypass.
/// </summary>
public interface IRetentionEnforcementService
{
    /// <summary>
    /// Enforces retention rules for a specific collection.
    /// Moves excess clips to Overflow or Trashcan based on collection type.
    /// Skips enforcement for ReadOnly collections.
    /// </summary>
    /// <param name="databaseKey">Database identifier.</param>
    /// <param name="collectionId">Collection to enforce retention on.</param>
    /// <returns>Number of clips moved/deleted.</returns>
    Task<int> EnforceRetentionAsync(string databaseKey, Guid collectionId);

    /// <summary>
    /// Enforces retention rules across all collections in the database.
    /// Typically called by scheduled maintenance service.
    /// </summary>
    /// <param name="databaseKey">Database identifier.</param>
    /// <returns>Total number of clips moved/deleted.</returns>
    Task<int> EnforceAllCollectionsAsync(string databaseKey);
}
