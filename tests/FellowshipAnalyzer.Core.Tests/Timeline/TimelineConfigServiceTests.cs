using FellowshipAnalyzer.Core.UI.Timeline;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Timeline;

public class TimelineConfigServiceTests
{
    [Fact]
    public void PreconfiguredAura_WithoutStoredEntry_IsAddedVisible()
    {
        var stored = new TimelineConfig();
        var preconfigured = new HashSet<int> { 100 };

        var merged = TimelineConfigService.MergeWithDefaults(
            stored,
            allCooldownIds: [],
            defaultHighlightedAuras: preconfigured,
            cooldownDefaultSortIndices: new Dictionary<int, int?>());

        var entry = merged.Auras.ShouldHaveSingleItem();
        entry.SpellId.ShouldBe(100);
        entry.Visible.ShouldBeTrue();
        entry.Priority.ShouldBe(0);
    }

    [Fact]
    public void PreconfiguredAura_AbsentFromLog_IsStillAdded()
    {
        var stored = new TimelineConfig();
        var preconfigured = new HashSet<int> { 200 };

        var merged = TimelineConfigService.MergeWithDefaults(
            stored,
            allCooldownIds: [],
            defaultHighlightedAuras: preconfigured,
            cooldownDefaultSortIndices: new Dictionary<int, int?>());

        merged.Auras.ShouldContain(a => a.SpellId == 200 && a.Visible);
    }

    [Fact]
    public void NonPreconfiguredAura_IsNotAutoAdded()
    {
        var stored = new TimelineConfig();
        var preconfigured = new HashSet<int> { 100 };

        var merged = TimelineConfigService.MergeWithDefaults(
            stored,
            allCooldownIds: [],
            defaultHighlightedAuras: preconfigured,
            cooldownDefaultSortIndices: new Dictionary<int, int?>());

        merged.Auras.ShouldNotContain(a => a.SpellId == 999);
        merged.Auras.Count.ShouldBe(1);
    }

    [Fact]
    public void StoredEntry_IsPreservedAcrossMerge()
    {
        var stored = new TimelineConfig
        {
            Auras = [new AuraConfigEntry(100, Visible: false, Priority: 5)],
        };
        var preconfigured = new HashSet<int> { 100 };

        var merged = TimelineConfigService.MergeWithDefaults(
            stored,
            allCooldownIds: [],
            defaultHighlightedAuras: preconfigured,
            cooldownDefaultSortIndices: new Dictionary<int, int?>());

        var entry = merged.Auras.ShouldHaveSingleItem();
        entry.Visible.ShouldBeFalse();
        entry.Priority.ShouldBe(5);
    }

    [Fact]
    public void CustomEntry_AbsentFromLog_IsPreserved()
    {
        var stored = new TimelineConfig
        {
            Auras = [new AuraConfigEntry(999, Visible: true, Priority: 3)],
        };

        var merged = TimelineConfigService.MergeWithDefaults(
            stored,
            allCooldownIds: [],
            defaultHighlightedAuras: new HashSet<int>(),
            cooldownDefaultSortIndices: new Dictionary<int, int?>());

        var entry = merged.Auras.ShouldHaveSingleItem();
        entry.SpellId.ShouldBe(999);
        entry.Visible.ShouldBeTrue();
        entry.Priority.ShouldBe(3);
    }

    [Fact]
    public void CooldownMerge_AddsInLogEntryWithSortIndex()
    {
        var stored = new TimelineConfig();
        var sortIndices = new Dictionary<int, int?> { [501] = 7 };

        var merged = TimelineConfigService.MergeWithDefaults(
            stored,
            allCooldownIds: [501],
            defaultHighlightedAuras: new HashSet<int>(),
            cooldownDefaultSortIndices: sortIndices);

        var entry = merged.Cooldowns.ShouldHaveSingleItem();
        entry.SpellId.ShouldBe(501);
        entry.Visible.ShouldBeTrue();
        entry.SortOrder.ShouldBe(7);
    }
}
