using FellowshipAnalyzer.Core.FellowshipLogs;
using Microsoft.JSInterop;

namespace FellowshipAnalyzer.Client.Services;

/// <summary>
/// IndexedDB-backed implementation of <see cref="IReportCacheService"/> for Blazor WebAssembly.
/// Events are stored as gzip-compressed UTF-8 JSON bytes to avoid giant-string JS interop.
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

    public async ValueTask<byte[]?> GetCachedEventsBytesAsync(string reportCode, int fightId, int playerId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<byte[]?>("getCachedEventsBytes", reportCode, fightId, playerId);
    }

    public async ValueTask CacheAsync(ReportHistoryEntry entry, byte[] eventsJsonBytes)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(
            "cacheEventsBytes",
            entry.ReportCode,
            entry.FightId,
            entry.PlayerId,
            eventsJsonBytes,
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

    public async ValueTask<string?> GetCachedMasterDataJsonAsync(string reportCode)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getCachedMasterData", reportCode);
    }

    public async ValueTask CacheMasterDataAsync(string reportCode, string masterDataJson)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("cacheMasterData", reportCode, masterDataJson);
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
