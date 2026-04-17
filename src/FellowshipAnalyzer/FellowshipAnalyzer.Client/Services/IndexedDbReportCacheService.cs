using FellowshipAnalyzer.Core.FellowshipLogs;
using Microsoft.JSInterop;

namespace FellowshipAnalyzer.Client.Services;

/// <summary>
/// IndexedDB-backed implementation of <see cref="IReportCacheService"/> for Blazor WebAssembly.
/// Events are stored as raw JSON strings to avoid redundant serialization/deserialization.
/// </summary>
internal sealed class IndexedDbReportCacheService(IJSRuntime js) : IReportCacheService, IAsyncDisposable
{
    private const string ModulePath = "./js/report-cache.js";
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        return _module;
    }

    public async ValueTask<string?> GetCachedEventsJsonAsync(string reportCode, int fightId, int playerId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getCachedEvents", reportCode, fightId, playerId);
    }

    public async ValueTask CacheAsync(ReportHistoryEntry entry, string eventsJson)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(
            "cacheEvents",
            entry.ReportCode,
            entry.FightId,
            entry.PlayerId,
            eventsJson,
            entry.FightName,
            entry.PlayerName,
            entry.HeroId);
    }

    public async ValueTask<IReadOnlyList<ReportHistoryEntry>> GetHistoryAsync()
    {
        var module = await GetModuleAsync();
        var raw = await module.InvokeAsync<IndexedDbHistoryEntry[]>("getHistory");
        return raw.Select(e => new ReportHistoryEntry(
            e.ReportCode,
            e.FightId,
            e.PlayerId,
            e.FightName,
            e.PlayerName,
            e.HeroId,
            DateTimeOffset.FromUnixTimeMilliseconds(e.CachedAt)
        )).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    // Matches the shape returned by the JS getHistory() function
    private sealed class IndexedDbHistoryEntry
    {
        public string ReportCode { get; set; } = "";
        public int FightId { get; set; }
        public int PlayerId { get; set; }
        public string? FightName { get; set; }
        public string? PlayerName { get; set; }
        public string? HeroId { get; set; }
        public long CachedAt { get; set; }
    }
}
