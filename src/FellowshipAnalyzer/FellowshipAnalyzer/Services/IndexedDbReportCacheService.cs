using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using Microsoft.JSInterop;

namespace FellowshipAnalyzer.Services;

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

    public async ValueTask<byte[]?> GetCachedEventsBytesAsync(string reportCode, int dungeonId, int playerId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<byte[]?>("getCachedEventsBytes", reportCode, dungeonId, playerId);
    }

    public async ValueTask CacheAsync(ReportHistoryEntry entry, byte[] eventsJsonBytes, DateTimeOffset? expiresAt = null)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(
            "cacheEventsBytes",
            entry.ReportCode,
            entry.DungeonId,
            entry.PlayerId,
            eventsJsonBytes,
            entry.DungeonName,
            entry.PlayerName,
            entry.Hero?.ToHeroId(),
            expiresAt.HasValue ? (long?)expiresAt.Value.ToUnixTimeMilliseconds() : null);
    }

    public async ValueTask<List<ReportHistoryEntry>> GetHistoryAsync()
    {
        var module = await GetModuleAsync();
        var raw = await module.InvokeAsync<IndexedDbHistoryEntry[]>("getHistory");
        return [.. raw.Select(e => new ReportHistoryEntry(
            e.ReportCode,
            e.DungeonId,
            e.PlayerId,
            e.DungeonName,
            e.PlayerName,
            Hero.TryParse(e.HeroId, out var hero) ? hero.Name : null,
            DateTimeOffset.FromUnixTimeMilliseconds(e.CachedAt)
        ))];
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

    private sealed class IndexedDbHistoryEntry
    {
        public string ReportCode { get; set; } = "";
        public int DungeonId { get; set; }
        public int PlayerId { get; set; }
        public string? DungeonName { get; set; }
        public string? PlayerName { get; set; }
        public string? HeroId { get; set; }
        public long CachedAt { get; set; }
    }
}
