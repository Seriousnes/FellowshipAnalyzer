using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Mara.Statistics;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// Tracks Deadly Scheme across the whole fight. Spending Energy banks one stack per five Energy, and
/// at <see cref="MaxStacks"/> the stacks convert into Deadly Scheme: Active - twelve seconds during
/// which every strike crits. Each activation is scored on what landed inside it: Queen's Fang and
/// Arachnid Assault are the finishers the window is banked for, and Hemorrhaging Strike benefits too,
/// so it is counted alongside them but reported separately.
/// <para>
/// Stack counts follow the buff stream (apply, stack, remove), preferring the count a stack event
/// carries over an increment. An activation whose removal is never logged is closed at the fabricated
/// <see cref="FightEndEvent"/>, which always dispatches last.
/// </para>
/// </summary>
[RequiresTalent(MaraTalents.DeadlyScheme)]
public sealed partial class DeadlySchemeTracker : EventSubscriber
{
    /// <summary>Stacks Deadly Scheme banks before converting into its active buff.</summary>
    public const int MaxStacks = 40;

    private int _stacks;
    private int? _activeSince;
    private bool _activationUsed;

    /// <summary>Highest Deadly Scheme stack count observed on the player.</summary>
    public int HighestStacks { get; private set; }

    /// <summary>Times the banked stacks converted into Deadly Scheme: Active.</summary>
    public int Activations { get; private set; }

    /// <summary>Total time Deadly Scheme: Active stood, in milliseconds.</summary>
    public int ActiveTimeMs { get; private set; }

    /// <summary>Queen's Fang and Arachnid Assault casts made while Deadly Scheme: Active stood.</summary>
    public int FinishersDuringActive { get; private set; }

    /// <summary>Hemorrhaging Strike casts made while Deadly Scheme: Active stood.</summary>
    public int HemorrhagingStrikesDuringActive { get; private set; }

    /// <summary>
    /// Activations that carried at least one benefiting cast - a Queen's Fang, an Arachnid Assault or
    /// a Hemorrhaging Strike.
    /// </summary>
    public int ActivationsSpent { get; private set; }

    /// <summary>Activations that ended without a single benefiting cast.</summary>
    public int ActivationsUnspent { get; private set; }

    public override Type? StatisticsComponentType =>
        Activations > 0 || HighestStacks > 0 ? typeof(DeadlySchemeStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeStacks))]
    private void OnStacksApplied(ApplyBuffEvent buffEvent) => SetStacks(1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeStacks))]
    private void OnStackGained(ApplyBuffStackEvent buffEvent) =>
        SetStacks(buffEvent.Stack > 0 ? buffEvent.Stack : _stacks + 1);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeStacks))]
    private void OnStacksRemoved(RemoveBuffEvent buffEvent) => SetStacks(0);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeActive))]
    private void OnActiveApplied(ApplyBuffEvent buffEvent)
    {
        CloseActivation(buffEvent.Timestamp);
        Activations++;
        _activeSince = buffEvent.Timestamp;
        _activationUsed = false;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeActive))]
    private void OnActiveRemoved(RemoveBuffEvent buffEvent) => CloseActivation(buffEvent.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.QueenFang), nameof(Spells.ArachnidAssault)])]
    private void OnFinisherCast(CastEvent castEvent)
    {
        if (castEvent.Fake || _activeSince is null) return;

        FinishersDuringActive++;
        _activationUsed = true;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HemorrhagingStrike))]
    private void OnHemorrhagingStrikeCast(CastEvent castEvent)
    {
        if (castEvent.Fake || _activeSince is null) return;

        HemorrhagingStrikesDuringActive++;
        _activationUsed = true;
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent fightEndEvent) => CloseActivation(fightEndEvent.Timestamp);

    private void SetStacks(int stacks)
    {
        _stacks = Math.Max(0, stacks);
        HighestStacks = Math.Max(HighestStacks, _stacks);
    }

    private void CloseActivation(int timestamp)
    {
        if (_activeSince is not { } openedAt) return;

        ActiveTimeMs += Math.Max(0, timestamp - openedAt);
        if (_activationUsed)
            ActivationsSpent++;
        else
            ActivationsUnspent++;

        _activeSince = null;
        _activationUsed = false;
    }
}
