using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Chrona Tap.</summary>
public interface IChronaTapAnalyzer : IAnalyzerSurface;

/// <summary>The Chrona Tap stack count at one instant.</summary>
/// <param name="Timestamp">When the count changed.</param>
/// <param name="Stacks">The stack count from this instant until the next change.</param>
public readonly record struct ChronaTapSample(int Timestamp, int Stacks);

/// <summary>One Chrona spender cast at maximum stacks.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="AbilityId">The spender's FSLID.</param>
/// <param name="ManaLost">Mana lost at the cap.</param>
public sealed record ChronaTapOvercap(int Timestamp, int AbilityId, int ManaLost);

/// <summary>Chrona Tap over one pull.</summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(AeonaTalents.ChronaTap)]
[Dependency<Abilities>]
[Dependency<ChronaTracker>]
public sealed partial class ChronaTapAnalyzer : Analyzer, IChronaTapAnalyzer
{
    /// <summary>The stacks Chrona Tap holds at most.</summary>
    public const int MaximumStacks = 10;

    private readonly List<ChronaTapSample> _history = [];
    private readonly List<ChronaTapOvercap> _overcaps = [];

    private int _stacks;

    /// <summary>Stacks Chrona Tap gained during the pull.</summary>
    public int StacksGained { get; private set; }

    /// <summary>Chrona spender casts during the pull.</summary>
    public int SpenderCasts { get; private set; }

    /// <summary>Stacks gained per Chrona spender cast, on average.</summary>
    public double StacksPerSpender => SpenderCasts == 0 ? 0 : (double)StacksGained / SpenderCasts;

    /// <summary>The stack count at every change during the pull, in order.</summary>
    public IReadOnlyList<ChronaTapSample> StackHistory => _history;

    /// <summary>Every Chrona spender cast at maximum stacks, in cast order.</summary>
    public IReadOnlyList<ChronaTapOvercap> Overcaps => _overcaps;

    /// <summary>Chrona spender casts at maximum stacks.</summary>
    public int SpendersAtMaximumStacks => _overcaps.Count;

    /// <summary>Share of Chrona spender casts (0-1) at maximum stacks.</summary>
    public double SpendersAtMaximumStacksShare =>
        SpenderCasts == 0 ? 0 : (double)SpendersAtMaximumStacks / SpenderCasts;

    /// <summary>Stacks lost at the cap.</summary>
    public int StacksLostAtCap => _overcaps.Count;

    /// <summary>Mana lost at the cap.</summary>
    public int ManaLostAtCap => _overcaps.Sum(overcap => overcap.ManaLost);

    /// <summary>The mana Chrona Tap's expiries returned during the pull.</summary>
    public int ManaReturned { get; private set; }

    /// <summary>The mana one stack returns at expiry.</summary>
    public int ManaPerStack => (int)Math.Round(PerStackShare * ChronaTracker.MaxOf(ResourceTypes.Mana));

    private static double PerStackShare => Talents.ChronaTap.ResourceGeneration?.Amount ?? 0;

    [On<CastEvent>(By = Actor.Player)]
    private void OnPlayerCast(CastEvent e)
    {
        if (!SpendsChrona(e.Ability.FSLID)) return;

        SpenderCasts++;

        if (_stacks < MaximumStacks) return;

        _overcaps.Add(new ChronaTapOvercap(e.Timestamp, e.Ability.FSLID, ManaPerStack));
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnApplied(ApplyBuffEvent e) => RecordStacks(e.Timestamp, 1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnStackApplied(ApplyBuffStackEvent e) => RecordStacks(e.Timestamp, e.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnStackRemoved(RemoveBuffStackEvent e) => Expire(e.Timestamp, e.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnRemoved(RemoveBuffEvent e) => Expire(e.Timestamp, 0);

    private void Expire(int timestamp, int remaining)
    {
        var capped = Math.Clamp(remaining, 0, MaximumStacks);
        if (capped < _stacks) ManaReturned += (_stacks - capped) * ManaPerStack;

        RecordStacks(timestamp, capped);
    }

    private void RecordStacks(int timestamp, int stacks)
    {
        var capped = Math.Clamp(stacks, 0, MaximumStacks);
        if (capped > _stacks) StacksGained += capped - _stacks;

        _stacks = capped;
        _history.Add(new ChronaTapSample(timestamp, capped));
    }

    private bool SpendsChrona(FSLID abilityId) =>
        Abilities.GetAbility(abilityId)?.PrimarySpell.Cost(ResourceTypes.Primary) is not null;
}
