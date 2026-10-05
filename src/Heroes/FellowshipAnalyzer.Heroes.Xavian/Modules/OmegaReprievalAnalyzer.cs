using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Xavian;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Xavian.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class OmegaReprievalAnalyzer : Analyzer
{
    public const int ConsumeGraceMs = 250;

    private int _stacks;
    private int? _lastFlash;
    private bool _goldenHourActive;

    public int Casts { get; private set; }

    public int StacksGained { get; private set; }

    public int StacksSpent { get; private set; }

    public int StacksExpired { get; private set; }

    public int GoldenHourProcs { get; private set; }

    public int GoldenHourConsumed { get; private set; }

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent pullStart)
    {
        _stacks = Owner.SelectedCombatant.GetBuffStacks(Spells.OmegaReprievalBuff, pullStart.Timestamp);
        _goldenHourActive = Owner.SelectedCombatant.HasBuff(Spells.GoldenHour, pullStart.Timestamp);
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.OmegaReprieval))]
    private void OnCast() => Casts++;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.BrilliantFlash))]
    private void OnBrilliantFlash(CastEvent castEvent) => _lastFlash = castEvent.Timestamp;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Omnistrike))]
    private void OnOmnistrike()
    {
        if (!_goldenHourActive) return;

        GoldenHourConsumed++;
        _goldenHourActive = false;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OmegaReprievalBuff))]
    private void OnApplied(ApplyBuffEvent buffEvent) => Gain(Math.Max(1, _stacks));

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OmegaReprievalBuff))]
    private void OnStackGained(ApplyBuffStackEvent buffEvent) => Gain(buffEvent.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OmegaReprievalBuff))]
    private void OnStackLost(RemoveBuffStackEvent buffEvent) =>
        Lose(Math.Max(_stacks, buffEvent.Stack + 1), buffEvent.Stack, buffEvent.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OmegaReprievalBuff))]
    private void OnRemoved(RemoveBuffEvent buffEvent) => Lose(Math.Max(_stacks, 1), 0, buffEvent.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.GoldenHour))]
    private void OnGoldenHour()
    {
        GoldenHourProcs++;
        _goldenHourActive = true;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.GoldenHour))]
    private void OnGoldenHourRefreshed()
    {
        if (_goldenHourActive) GoldenHourProcs++;
        _goldenHourActive = true;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.GoldenHour))]
    private void OnGoldenHourRemoved() => _goldenHourActive = false;

    private void Gain(int stacks)
    {
        if (stacks > _stacks) StacksGained += stacks - _stacks;
        _stacks = stacks;
    }

    private void Lose(int prior, int stacks, int timestamp)
    {
        var lost = prior - stacks;
        if (lost > 0 && _lastFlash is { } flash && timestamp - flash <= ConsumeGraceMs)
        {
            StacksSpent++;
            StacksExpired += lost - 1;
            _lastFlash = null;
        }
        else if (lost > 0)
        {
            StacksExpired += lost;
        }

        _stacks = stacks;
    }
}
