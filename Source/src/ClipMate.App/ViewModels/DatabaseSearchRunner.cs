using System.Windows.Threading;
using ClipMate.App.Models;
using ClipMate.App.Services;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Runs the search dialog's searches against the Databases a <see cref="SearchTarget" /> covers, and logs them: each
/// Database searched gets the matches in its Search Results cache and node. One Database's failure fails its search;
/// "All Databases" skips the Databases it can't search and reports them in the status bar.
/// </summary>
internal sealed class DatabaseSearchRunner(
    IDatabases databases,
    ISearchService searchService,
    ICollectionService collectionService,
    SearchResultsCache searchResultsCache,
    RemoteSearchResultsCache remoteSearchResultsCache,
    IMessenger messenger,
    ILogger logger,
    TimeProvider timeProvider)
{
    private const string _activeDatabase = "the active Database";

    /// <summary>
    /// Searches what <paramref name="target" /> covers (<c>null</c>: the active Database) with <paramref name="filters" />,
    /// as narrowed by the target and as each Database can apply them.
    /// </summary>
    /// <param name="target">What "Search in" chose.</param>
    /// <param name="filters">The filters as the form is filled in, for every collection.</param>
    /// <param name="query">What the search is called in its Search Results node.</param>
    /// <param name="search">The running search, canceled by the dialog.</param>
    /// <param name="reportProgress">Told, during "All Databases", how many Databases have answered.</param>
    /// <returns>Every match found, in tree order of their Databases.</returns>
    public async Task<SearchMatches> RunAsync(SearchTarget? target, SearchFilters filters, string query, RunningSearch search, Action<string> reportProgress)
    {
        var description = target?.Describe() ?? _activeDatabase;
        var narrowed = target?.Narrow(filters) ?? filters;
        var started = timeProvider.GetTimestamp();
        logger.LogInformation("Searching {DatabaseKey}, scope {Scope}, for {Criteria}", description, narrowed.Scope, DescribeCriteria(narrowed));
        try
        {
            return target switch
            {
                null => await SearchActiveDatabaseAsync(narrowed, query, search.Token),
                AllDatabasesSearchTarget => await SearchEveryDatabaseAsync(narrowed, query, reportProgress, search.Token),
                DatabaseSearchTarget database => await SearchDatabaseAsync(database.DatabaseKey, narrowed, query, true, search.Token),
                var _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Not a kind of search target."),
            };
        }
        catch (OperationCanceledException) when (search.Token.IsCancellationRequested)
        {
            logger.LogInformation("Search of {DatabaseKey} canceled after {ElapsedMs:F0} ms: {Reason}", description,
                timeProvider.GetElapsedTime(started).TotalMilliseconds, search.CancelReason);
            throw;
        }
    }

    /// <summary>
    /// <paramref name="filters" /> without the ones only local Databases apply: Peers never serve deleted clips or
    /// shortcuts.
    /// </summary>
    public static SearchFilters WithoutLocalOnlyFilters(SearchFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return filters with { HasShortcutOnly = null, IncludeDeleted = false };
    }

    /// <summary>
    /// Searches the active Database when there is no Database to choose in "Search in".
    /// </summary>
    private async Task<SearchMatches> SearchActiveDatabaseAsync(SearchFilters filters, string query, CancellationToken cancellationToken)
    {
        // Pass empty string for query parameter since all search criteria are in filters
        var matches = await SearchLoggedAsync(_activeDatabase, async () =>
        {
            var results = await searchService.SearchAsync("", filters, cancellationToken);
            return new SearchMatches(results.Clips, results.TotalMatches);
        }, cancellationToken);

        if (collectionService.GetActiveDatabaseKey() is { Length: > 0 } databaseKey)
            ShowResults(databaseKey, query, matches, true);

        return matches;
    }

    /// <summary>
    /// Searches one Database with the filters it can apply, and gives its Search Results node the matches, even none, so
    /// it never shows an older query's matches beside them.
    /// </summary>
    /// <param name="selectsNode">Whether the collection tree then selects the node, to show the matches.</param>
    private async Task<SearchMatches> SearchDatabaseAsync(string databaseKey, SearchFilters filters, string query, bool selectsNode,
        CancellationToken cancellationToken)
    {
        // The Database searches itself: a local one with SQL, a Remote one on its Peer (criteria only, never SQL)
        var matches = await SearchLoggedAsync(databaseKey, () =>
        {
            var database = databases.Get(databaseKey);
            return database.SearchAsync(database.Remote == null ? filters : WithoutLocalOnlyFilters(filters), cancellationToken: cancellationToken);
        }, cancellationToken);

        ShowResults(databaseKey, query, matches, selectsNode);
        return matches;
    }

    /// <summary>
    /// Runs one Database's search, logging how it ended: the matches and how long it took, or why it failed. A
    /// cancellation is logged once, by <see cref="RunAsync" />.
    /// </summary>
    private async Task<SearchMatches> SearchLoggedAsync(string databaseKey, Func<Task<SearchMatches>> search, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        try
        {
            var matches = await search();
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Searched {DatabaseKey}: {Count} of {TotalMatches} matches in {ElapsedMs:F0} ms", databaseKey,
                matches.Clips.Count, matches.TotalMatches, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            return matches;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DatabaseUnavailableException ex)
        {
            logger.LogInformation("Search of {DatabaseKey} failed after {ElapsedMs:F0} ms: {Reason}", databaseKey,
                timeProvider.GetElapsedTime(started).TotalMilliseconds, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Search of {DatabaseKey} failed after {ElapsedMs:F0} ms", databaseKey, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Keeps a Database's matches for its Search Results node and tells the collection tree. A Remote Database's are
    /// kept with their clips, as they can't be read again by ID; the one place search reads Remote (spec Q22 exception,
    /// see database-seam ticket 09).
    /// </summary>
    private void ShowResults(string databaseKey, string query, SearchMatches matches, bool selectsNode)
    {
        var clipIds = matches.Clips.Select(p => p.Id).ToList();
        if (RemoteDatabaseKey.TryParse(databaseKey, out var address))
            remoteSearchResultsCache.SetResults(address, new RemoteSearchResult(query, matches.Clips, matches.TotalMatches));
        else
            searchResultsCache.SetResults(databaseKey, new SearchResult(query, clipIds));

        messenger.Send(new SearchExecutedEvent(databaseKey, query, clipIds) { SelectsNode = selectsNode });
    }

    /// <summary>
    /// Searches every available Database at once, local and remote. Each Database's matches (or none) go to its own
    /// Search Results node as it answers, without selecting it; a Database that is unavailable or fails is skipped and
    /// its node left as it was. When all have answered, the first Database in tree order with matches is selected and
    /// the status bar gives the total and the skipped Databases' reasons.
    /// </summary>
    private async Task<SearchMatches> SearchEveryDatabaseAsync(SearchFilters filters, string query, Action<string> reportProgress,
        CancellationToken cancellationToken)
    {
        var all = databases.All;
        var skipped = new Dictionary<IDatabase, string>();
        var searching = new Dictionary<Task<SearchMatches>, IDatabase>();
        foreach (var item in all)
        {
            if (item.IsAvailable)
                searching[SearchDatabaseAsync(item.Key, filters, query, false, cancellationToken)] = item;
            else
            {
                skipped[item] = item.Remote is { } remote ? $"{remote.PeerName} is offline" : $"the file of {item.Name} is missing";
                logger.LogInformation("Search of {DatabaseKey} skipped: {Reason}", item.Key, skipped[item]);
            }
        }

        var found = new Dictionary<IDatabase, SearchMatches>();
        var searchedCount = searching.Count;
        ReportProgress();
        while (searching.Count > 0)
        {
            var answered = await Task.WhenAny(searching.Keys);
            var database = searching[answered];
            searching.Remove(answered);
            try
            {
                found[database] = await answered;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The others stop too, but may still fail on the way (logged); nobody waits for them
                foreach (var item in searching.Keys)
                    _ = item.ContinueWith(static p => _ = p.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

                throw;
            }
            catch (Exception ex)
            {
                // Logged; an offline Peer or a missing file doesn't fail the search as a whole
                skipped[database] = ex.Message.TrimEnd('.');
            }

            ReportProgress();
        }

        var clips = all.Where(found.ContainsKey).SelectMany(p => found[p].Clips).ToList();
        var totalMatches = found.Values.Sum(p => p.TotalMatches);

        // One selection, at the end, so the tree doesn't jump with every answer
        if (all.FirstOrDefault(p => found.TryGetValue(p, out var matches) && matches.Clips.Count > 0) is { } first)
            messenger.Send(new SearchExecutedEvent(first.Key, query, found[first].Clips.Select(p => p.Id).ToList()));

        var summary = $"{totalMatches:N0} {(totalMatches == 1 ? "match" : "matches")} in {found.Count} {(found.Count == 1 ? "Database" : "Databases")}";
        if (skipped.Count > 0)
            summary += $". {skipped.Count} couldn't be searched: {string.Join("; ", all.Where(skipped.ContainsKey).Select(p => skipped[p]))}";

        logger.LogInformation("Searched {Count} of {DatabaseCount} Databases: {Summary}", found.Count, all.Count, summary);
        ShowSummaryAfterTheResults(summary);
        return new SearchMatches(clips, totalMatches);

        void ReportProgress()
        {
            var stillSearching = all.Where(searching.ContainsValue).Select(p => SearchTarget.For(p).Name).ToList();
            reportProgress(stillSearching.Count == 0
                ? $"Searched {searchedCount} of {searchedCount} Databases"
                : $"Searched {searchedCount - stillSearching.Count} of {searchedCount} Databases… still searching {string.Join(", ", stillSearching)}");
        }
    }

    /// <summary>
    /// Shows an "All Databases" summary in the status bar after the tree has selected a Search Results node, kept while
    /// the clip list loads that node's clips and selects the first, until the user picks a clip or node.
    /// </summary>
    private void ShowSummaryAfterTheResults(string summary)
    {
        // Queued at a low priority, which UiThread.Run doesn't give: the tree selects the node at normal priority, and
        // that selection would otherwise end the kept summary
        var status = new StatusUpdateEvent(summary) { KeepUntilUserSelects = true };
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            messenger.Send(status);
        else
            dispatcher.BeginInvoke(() => messenger.Send(status), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Names the criteria a search uses, for the log. Never the text searched for, which may be private.
    /// </summary>
    private static string DescribeCriteria(SearchFilters filters)
    {
        var criteria = new List<string>();
        if (filters.TitleQuery != null)
            criteria.Add("title");

        if (filters.TextContentQuery != null)
            criteria.Add("clip text");

        if (filters.CreatorQuery != null)
            criteria.Add("creator");

        if (filters.SourceUrlQuery != null)
            criteria.Add("source URL");

        if (filters.Format != null)
            criteria.Add("format");

        if (filters.ContentTypes != null)
            criteria.Add($"types {string.Join("/", filters.ContentTypes)}");

        if (filters.DateRange != null)
            criteria.Add("date range");

        if (filters.EncryptedOnly == true)
            criteria.Add("encrypted only");

        if (filters.HasShortcutOnly == true)
            criteria.Add("with shortcut only");

        if (filters.IncludeDeleted)
            criteria.Add("including deleted");

        if (filters.IsRegex)
            criteria.Add("regex");

        if (filters.CaseSensitive)
            criteria.Add("case-sensitive");

        return criteria.Count == 0 ? "everything" : string.Join(", ", criteria);
    }
}
