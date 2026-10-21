namespace ClipMate.Core.Models.Networking;

/// <summary>
/// A Database a Device serves to its Peers (its Share with Peers setting is on), as listed to them.
/// </summary>
/// <param name="DatabaseId">The permanent Database ID stored in the database file.</param>
/// <param name="Name">The Database's name on the Device that shares it.</param>
public sealed record SharedDatabase(Guid DatabaseId, string Name);
