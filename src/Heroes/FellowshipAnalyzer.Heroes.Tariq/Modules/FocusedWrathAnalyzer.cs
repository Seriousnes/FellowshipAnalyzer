using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FocusedWrathAnalyzer : Analyzer
{
    public const int BuffDurationMs = 15_000;

    public const int MaxStackLimit = 3;

    public const int StacksPerCast = 2;

    public const int ConsumptionGraceMs = 500;

    private static readonly int SkullCrusherId = Spells.SkullCrusher.FSLID;
    private static readonly int HammerStormId = Spells.HammerStorm.FSLID;

    private readonly List<OpenWindow> _windows = [];
    private readonly List<SpenderCast> _spenders = [];

    private OpenWindow? _open;

    private List<FocusedWrathWindow> Evaluated => field ??= Build();

    public List<FocusedWrathWindow> Windows => Evaluated;

    public int WindowCount => Evaluated.Count;

    public bool SchismTalented => Owner.SelectedCombatant.HasTalent(TariqTalents.Schism);

    public int PreferredSpenderId =>
        SchismTalented || Pull.Targets == PullKind.Multi ? HammerStormId : SkullCrusherId;

    public int ChargesGranted => Evaluated.Sum(window => window.ChargesGranted);

    public int ChargesConsumed => Evaluated.Sum(window => window.ChargesConsumed);

    public int ChargesWasted => Evaluated.Sum(window => window.ChargesWasted);

    public int ChargesFromCast => Evaluated.Count(window => window.FromCast) * StacksPerCast;

    public int ChargesFromProc => ChargesGranted - ChargesFromCast;

    public int CorrectSpenders => CountChoice(SpenderChoice.Correct);

    public int WrongSpenders => CountChoice(SpenderChoice.Wrong);

    public int UnratedConsumptions => CountChoice(SpenderChoice.Unrated);

    public int SkullCrusherConsumptions => CountConsumers(SkullCrusherId);

    public int HammerStormConsumptions => CountConsumers(HammerStormId);

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SkullCrusher),
        nameof(Spells.HammerStorm),
    })]
    private void OnSpenderCast(CastEvent @event)
    {
        if (@event.Fake)
            return;

        _spenders.Add(new SpenderCast(@event.Timestamp, @event.Ability.Id));
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FocusedWrathSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent @event)
    {
        Close(@event.Timestamp);
        Open(@event.Timestamp);
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.FocusedWrathSelfBuff))]
    private void OnChargeGranted(ApplyBuffStackEvent @event)
    {
        if (_open is null)
        {
            Open(@event.Timestamp);
            return;
        }

        _open.ChargesGranted++;

        if (@event.Timestamp == _open.OpenedAt)
            _open.FromCast = true;
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.FocusedWrathSelfBuff))]
    private void OnChargeRemoved()
    {
        if (_open is not null)
            _open.StacksRemoved++;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FocusedWrathSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent @event) => Close(@event.Timestamp);

    private void Open(int timestamp)
    {
        _open = new OpenWindow(timestamp);
        _windows.Add(_open);
    }

    private void Close(int timestamp)
    {
        if (_open is null)
            return;

        _open.ClosedAt = timestamp;
        _open = null;
    }

    private int CountChoice(SpenderChoice choice) =>
        Evaluated.Sum(window => window.Consumptions.Count(consumption => consumption.Choice == choice));

    private int CountConsumers(int spellId) =>
        Evaluated.Sum(window => window.Consumptions.Count(consumption => consumption.SpenderSpellId == spellId));

    private List<FocusedWrathWindow> Build()
    {
        var built = new List<FocusedWrathWindow>(_windows.Count);
        var preferred = PreferredSpenderId;
        var cursor = 0;

        for (var index = 0; index < _windows.Count; index++)
        {
            var window = _windows[index];
            var clipped = window.ClosedAt is null;
            var closedAt = window.ClosedAt ?? Pull.EndTime;
            var remaining = window.ChargesGranted - window.StacksRemoved;
            var lastChargeSpent = !clipped && remaining == 1 && closedAt - window.OpenedAt < BuffDurationMs;
            var consumed = window.StacksRemoved + (lastChargeSpent ? 1 : 0);

            var matchEnd = closedAt + ConsumptionGraceMs;
            if (index + 1 < _windows.Count)
                matchEnd = Math.Min(matchEnd, _windows[index + 1].OpenedAt);

            while (cursor < _spenders.Count && _spenders[cursor].Timestamp < window.OpenedAt)
                cursor++;

            var consumptions = new List<FocusedWrathConsumption>(consumed);

            while (consumptions.Count < consumed
                && cursor < _spenders.Count
                && _spenders[cursor].Timestamp <= matchEnd)
            {
                var spender = _spenders[cursor++];

                consumptions.Add(new FocusedWrathConsumption
                {
                    Timestamp = spender.Timestamp,
                    SpenderSpellId = spender.SpellId,
                    Choice = spender.SpellId == preferred ? SpenderChoice.Correct : SpenderChoice.Wrong,
                });
            }

            while (consumptions.Count < consumed)
                consumptions.Add(new FocusedWrathConsumption { Choice = SpenderChoice.Unrated });

            built.Add(new FocusedWrathWindow
            {
                OpenedAt = window.OpenedAt,
                ClosedAt = closedAt,
                ClippedByPullEnd = clipped,
                FromCast = window.FromCast,
                ChargesGranted = window.ChargesGranted,
                PreferredSpenderId = preferred,
                Consumptions = consumptions,
            });
        }

        return built;
    }

    private sealed class OpenWindow(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int? ClosedAt { get; set; }
        public int ChargesGranted { get; set; } = 1;
        public int StacksRemoved { get; set; }
        public bool FromCast { get; set; }
    }

    private readonly record struct SpenderCast(int Timestamp, int SpellId);
}

public enum SpenderChoice
{
    Unrated,

    Correct,

    Wrong,
}

public sealed record FocusedWrathConsumption
{
    public int? Timestamp { get; init; }

    public int? SpenderSpellId { get; init; }

    public required SpenderChoice Choice { get; init; }
}

public sealed record FocusedWrathWindow
{
    public required int OpenedAt { get; init; }

    public required int ClosedAt { get; init; }

    public int DurationMs => ClosedAt - OpenedAt;

    public required bool ClippedByPullEnd { get; init; }

    public required bool FromCast { get; init; }

    public required int ChargesGranted { get; init; }

    public required int PreferredSpenderId { get; init; }

    public List<FocusedWrathConsumption> Consumptions { get; init; } = [];

    public int ChargesConsumed => Consumptions.Count;

    public int ChargesWasted => ClippedByPullEnd ? 0 : ChargesGranted - ChargesConsumed;

    public int CorrectSpenders => Consumptions.Count(consumption => consumption.Choice == SpenderChoice.Correct);

    public int WrongSpenders => Consumptions.Count(consumption => consumption.Choice == SpenderChoice.Wrong);

    public int RatedSpenders => CorrectSpenders + WrongSpenders;
}
