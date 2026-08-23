using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

[RequiresTalent(MaraTalents.DeadlyScheme)]
public sealed partial class DeadlySchemeTracker : Analyzer
{
    public const int MaxStacks = 40;

    private int _stacks;
    private int? _activeSince;
    private bool _activationUsed;

    public int HighestStacks { get; private set; }

    public int Activations { get; private set; }

    public int ActiveTimeMs { get; private set; }

    public int FinishersDuringActive { get; private set; }

    public int HemorrhagingStrikesDuringActive { get; private set; }

    public int ActivationsSpent { get; private set; }

    public int ActivationsUnspent { get; private set; }

    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeStacks))]
    private void OnStacksApplied() => SetStacks(1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeStacks))]
    private void OnStackGained(ApplyBuffStackEvent buffEvent) =>
        SetStacks(buffEvent.Stack > 0 ? buffEvent.Stack : _stacks + 1);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.DeadlySchemeStacks))]
    private void OnStacksRemoved() => SetStacks(0);

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

    [On<DungeonEndEvent>]
    private void OnDungeonEnd(DungeonEndEvent dungeonEndEvent) => CloseActivation(dungeonEndEvent.Timestamp);

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
