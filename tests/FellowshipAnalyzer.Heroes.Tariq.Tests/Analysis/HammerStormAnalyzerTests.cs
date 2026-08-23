using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;
using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class HammerStormAnalyzerTests
{
    private const int PlayerId = 7;
    private const int Enemy1 = 11;
    private const int Enemy2 = 12;
    private const int Enemy3 = 13;

    private static readonly int HammerStormId = TariqSpells.HammerStorm.FSLID;
    private static readonly int SchismHammerStorm = TariqSpells.SchismHammerStorm.FSLID;
    private static readonly int SchismSkullCrusher = TariqSpells.SchismSkullCrusher.FSLID;

    [Fact]
    public async Task Analyze_HammerStorm_ClustersEachSpinsMultiTargetBurstIntoOneSpin()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Hit(1_052, Enemy2),
            Hit(1_055, Enemy3),
            Hit(1_400, Enemy1),
            Hit(1_403, Enemy2),
            Hit(1_750, Enemy1),
            Hit(1_752, Enemy2),
            Hit(1_754, Enemy3),
        ]);

        var analyzer = Analyzer(parser);
        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.Timestamp.ShouldBe(1_000);
        cast.SpinsCompleted.ShouldBe(HammerStormAnalyzer.ExpectedSpins);
        cast.TargetsHit.ShouldBe(3);
        cast.Truncated.ShouldBeFalse();
        cast.NextAbilityId.ShouldBeNull();

        analyzer.CastCount.ShouldBe(1);
        analyzer.CompleteChannels.ShouldBe(1);
        analyzer.TruncatedChannels.ShouldBe(0);
        analyzer.WhiffedCasts.ShouldBe(0);
        analyzer.AverageTargetsHit.ShouldBe(3);
    }

    /// <summary>
    /// A channel that connected with fewer targets than <see cref="HammerStormAnalyzer.TargetBreakEven"/>
    /// is marked, one that reached it exactly is not, and a whiff carries no readable target count so it
    /// is left unmarked rather than counted against the break-even.
    /// </summary>
    [Fact]
    public async Task Analyze_HammerStorm_MarksChannelsUnderTheTargetBreakEven()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Hit(1_052, Enemy2),
            Cast(10_000, HammerStormId),
            Hit(10_050, Enemy1),
            Hit(10_052, Enemy2),
            Hit(10_054, Enemy3),
            Cast(20_000, HammerStormId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.CastCount.ShouldBe(3);
        analyzer.UnderBreakEvenChannels.ShouldBe(1);
        analyzer.WhiffedCasts.ShouldBe(1);

        analyzer.Casts[0].TargetsHit.ShouldBe(2);
        analyzer.Casts[0].UnderTargetBreakEven.ShouldBeTrue();
        analyzer.Casts[1].TargetsHit.ShouldBe(HammerStormAnalyzer.TargetBreakEven);
        analyzer.Casts[1].UnderTargetBreakEven.ShouldBeFalse();
        analyzer.Casts[2].TargetsHit.ShouldBe(0);
        analyzer.Casts[2].UnderTargetBreakEven.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_HammerStorm_GroupsChannelsByTargetsCaughtLeavingWhiffsOut()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Cast(10_000, HammerStormId),
            Hit(10_050, Enemy1),
            Cast(20_000, HammerStormId),
            Hit(20_050, Enemy1),
            Hit(20_052, Enemy2),
            Hit(20_054, Enemy3),
            Cast(30_000, HammerStormId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.CastCount.ShouldBe(4);
        analyzer.WhiffedCasts.ShouldBe(1);
        analyzer.TargetsHitDistribution.ShouldBe(
        [
            new TargetCountBucket(1, 2),
            new TargetCountBucket(3, 1),
        ]);
    }

    [Fact]
    public async Task Analyze_HammerStorm_CountsTargetsOnTheFirstSpinAlone()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Hit(1_400, Enemy1),
            Hit(1_402, Enemy2),
            Hit(1_404, Enemy3),
            Hit(1_750, Enemy1),
            Hit(1_752, Enemy2),
        ]);

        var cast = Analyzer(parser).Casts.ShouldHaveSingleItem();

        cast.SpinsCompleted.ShouldBe(3);
        cast.TargetsHit.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_HammerStorm_ClustersDamageLoggedUnderTheChannelsEffectIds()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1, abilityId: TariqSpells.HammerStormDamage.FSLID),
            Hit(1_400, Enemy1, abilityId: TariqSpells.HammerStormLightningDamage.FSLID),
            Hit(1_750, Enemy1),
        ]);

        var cast = Analyzer(parser).Casts.ShouldHaveSingleItem();

        cast.SpinsCompleted.ShouldBe(3);
        cast.TargetsHit.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_HammerStorm_BlamesTheOffGlobalAbilityThatCutTheChannelShort()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Hit(1_053, Enemy2),
            Hit(1_400, Enemy1),
            Hit(1_403, Enemy2),
            Cast(1_500, TariqSpells.CullingStrike.FSLID),
        ]);

        var analyzer = Analyzer(parser);
        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.SpinsCompleted.ShouldBe(2);
        cast.TargetsHit.ShouldBe(2);
        cast.Truncated.ShouldBeTrue();
        cast.NextAbilityId.ShouldBe(TariqSpells.CullingStrike.FSLID);

        analyzer.TruncatedChannels.ShouldBe(1);
        analyzer.CompleteChannels.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_HammerStorm_LeavesATruncationUnattributedWhenNothingWasCastInsideTheChannel()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Hit(1_400, Enemy1),
            Cast(9_000, TariqSpells.SkullCrusher.FSLID),
        ]);

        var cast = Analyzer(parser).Casts.ShouldHaveSingleItem();

        cast.Truncated.ShouldBeTrue();
        cast.NextAbilityId.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_HammerStorm_IgnoresACastMadeBeforeTheChannelsLastSpin()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Cast(1_200, TariqSpells.LeapSmash.FSLID),
            Hit(1_400, Enemy1),
        ]);

        var cast = Analyzer(parser).Casts.ShouldHaveSingleItem();

        cast.SpinsCompleted.ShouldBe(2);
        cast.Truncated.ShouldBeTrue();
        cast.NextAbilityId.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_HammerStorm_TreatsACastThatDealtNoDamageAsAWhiffRatherThanATruncation()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Cast(2_000, TariqSpells.CullingStrike.FSLID),
        ]);

        var analyzer = Analyzer(parser);
        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.TargetsHit.ShouldBe(0);
        cast.SpinsCompleted.ShouldBe(0);
        cast.Truncated.ShouldBeFalse();
        cast.NextAbilityId.ShouldBeNull();

        analyzer.WhiffedCasts.ShouldBe(1);
        analyzer.TruncatedChannels.ShouldBe(0);
        analyzer.AverageTargetsHit.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_HammerStorm_StopsAttributingDamageAtTheNextHammerStormCast()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Cast(1_200, HammerStormId),
            Hit(1_250, Enemy1),
            Hit(1_600, Enemy1),
            Hit(1_950, Enemy1),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.Casts.Count.ShouldBe(2);
        analyzer.Casts[0].SpinsCompleted.ShouldBe(1);
        analyzer.Casts[0].Truncated.ShouldBeTrue();
        analyzer.Casts[0].NextAbilityId.ShouldBeNull();
        analyzer.Casts[1].SpinsCompleted.ShouldBe(3);
    }

    [Fact]
    public async Task Analyze_HammerStorm_LeavesDamagePastTheChannelCapOutOfTheSpinCount()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Hit(1_400, Enemy1),
            Hit(1_750, Enemy1),
            Hit(1_000 + HammerStormAnalyzer.MaxChannelDurationMs + 100, Enemy1),
        ]);

        Analyzer(parser).Casts.ShouldHaveSingleItem().SpinsCompleted.ShouldBe(3);
    }

    [Fact]
    public async Task Analyze_HammerStorm_MarksAChannelEmpoweredByAHeldSchismProc()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(500, SchismHammerStorm),
            Cast(1_000, HammerStormId),
            Remove(1_000, SchismHammerStorm),
            Hit(1_050, Enemy1),
            Hit(1_400, Enemy1),
            Hit(1_750, Enemy1),
        ]);

        var analyzer = Analyzer(parser);
        var cast = analyzer.Casts.ShouldHaveSingleItem();

        cast.SchismEmpowered.ShouldBeTrue();
        cast.TargetsHit.ShouldBe(1);

        analyzer.SchismEmpoweredCasts.ShouldBe(1);
        analyzer.HammerStormProcs.ShouldBe(new SchismProcEconomy(Gained: 1, Consumed: 1, Overwritten: 0, Expired: 0));
    }

    [Fact]
    public async Task Analyze_HammerStorm_ConsumesAProcRemovedAtTheSameMillisecondAsTheCast()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SchismHammerStorm),
            Remove(2_000, SchismHammerStorm),
            Cast(2_000, HammerStormId),
            Hit(2_050, Enemy1),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.Casts.ShouldHaveSingleItem().SchismEmpowered.ShouldBeTrue();
        analyzer.HammerStormProcs.ShouldBe(new SchismProcEconomy(Gained: 1, Consumed: 1, Overwritten: 0, Expired: 0));
    }

    [Fact]
    public async Task Analyze_HammerStorm_CountsTheProcEconomyInBothDirections()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SchismHammerStorm),
            Apply(1_100, SchismSkullCrusher),
            Cast(1_200, TariqSpells.SkullCrusher.FSLID),
            Remove(1_200, SchismSkullCrusher),
            Refresh(1_500, SchismHammerStorm),
            Cast(2_000, HammerStormId),
            Remove(2_000, SchismHammerStorm),
            Apply(3_000, SchismHammerStorm),
            Apply(4_000, SchismSkullCrusher),
            Remove(9_000, SchismHammerStorm),
            Remove(10_000, SchismSkullCrusher),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.HammerStormProcs.ShouldBe(new SchismProcEconomy(Gained: 2, Consumed: 1, Overwritten: 1, Expired: 1));
        analyzer.SkullCrusherProcs.ShouldBe(new SchismProcEconomy(Gained: 2, Consumed: 1, Overwritten: 0, Expired: 1));
    }

    [Fact]
    public async Task Analyze_HammerStorm_ExcludesSchismEmpoweredCastsFromTheLowTargetCount()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
            Hit(1_400, Enemy1),
            Hit(1_750, Enemy1),
            Apply(9_000, SchismHammerStorm),
            Cast(10_000, HammerStormId),
            Remove(10_000, SchismHammerStorm),
            Hit(10_050, Enemy1),
            Hit(10_400, Enemy1),
            Hit(10_750, Enemy1),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.CastCount.ShouldBe(2);
        analyzer.Casts.Select(cast => cast.SchismEmpowered).ShouldBe([false, true]);
        analyzer.SchismEmpoweredCasts.ShouldBe(1);
        analyzer.AverageTargetsHit.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_HammerStorm_ReadsSchismFromTheSelectedCombatantsTalents()
    {
        var (talented, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Combatant(schism: true),
            Cast(1_000, HammerStormId),
        ]);

        var (untalented, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Combatant(schism: false),
            Cast(1_000, HammerStormId),
        ]);

        Analyzer(talented).SchismTalented.ShouldBeTrue();
        Analyzer(untalented).SchismTalented.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_HammerStorm_ExposesPerPullReadPaths()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, HammerStormId),
            Hit(1_050, Enemy1),
        ]);

        var entry = parser.HammerStormAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.HammerStormAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).HammerStormAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static HammerStormAnalyzer Analyzer(TariqCombatLogParser parser) =>
        parser.HammerStormAnalyzers.ShouldHaveSingleItem().Analyzer;

    private static CastEvent Cast(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.CastWithoutResources(timestamp, abilityId);

    private static ApplyBuffEvent Apply(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(timestamp, abilityId);

    private static RefreshBuffEvent Refresh(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.Buff<RefreshBuffEvent>(timestamp, abilityId);

    private static RemoveBuffEvent Remove(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(timestamp, abilityId);

    /// <summary>
    /// One damage event of a Hammer Storm spin. Every hit of a burst shares a target instance so the
    /// distinct-target count keys off the unit alone.
    /// </summary>
    private static DamageEvent Hit(int timestamp, int targetId, int? abilityId = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = abilityId ?? HammerStormId, Name = "Hammer Storm" },
    };

    private static CombatantInfoEvent Combatant(bool schism) => new()
    {
        SourceId = PlayerId,
        Talents = schism ? [new TalentInfo { Id = 2_000_000 + TariqTalents.Schism }] : [],
    };
}
