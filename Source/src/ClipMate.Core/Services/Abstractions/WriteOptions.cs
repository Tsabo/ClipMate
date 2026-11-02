namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// How a write treats the Outbound Filter.
/// </summary>
/// <param name="Filter">Whether the clip is filtered to its plain text, or its image when it has no text.</param>
public readonly record struct WriteOptions(FilterMode Filter = FilterMode.FromSettings);
