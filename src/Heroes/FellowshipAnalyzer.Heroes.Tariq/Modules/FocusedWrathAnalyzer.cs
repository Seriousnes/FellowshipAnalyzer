using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FocusedWrathAnalyzer : Analyzer
{
    /// <summary>Buff lifetime from <c>Focused Wrath.Duration</c>.</summary>
    public const int BuffDurationMs = 15_000;

    /// <summary>Stack ceiling from <c>Focused Wrath.MaxStacksLimit</c>.</summary>
    public const int MaxStackLimit = 3;

    /// <summary>Charges a Focused Wrath cast grants, from <c>Focused Wrath.NumOfStacks</c>.</summary>
    public const int StacksPerCast = 2;

    /// <summary>Grace after a charge leaves the buff within which a spender cast still counts as the one that took it; the removal and the consuming cast share a timestamp in practice.</summary>
    public const int ConsumptionGraceMs = 500;

    private static readonly int SkullCrusherId = Spells.SkullCrusher.FSLID;
    private static readonly int HammerStormId = Spells.HammerStorm.FSLID;

    private readonly List<OpenWindow> _windows = [];
    private readonly List<SpenderCast> _spenders = [];

    private OpenWindow? _open;

    private List<FocusedWrathWindow> Evaluated => field ??= Build();

    public IReadOnlyList<FocusedWrathWindow> Windows => Evaluated;

    public int WindowCount => Evaluated.Count;

    /// <summary>Whether the player took <c>Schism</c>, which makes Hammer Storm the spender to aim a charge at on every pull shape.</summary>
    public bool SchismTalented => Owner.SelectedCombatant.HasTalent(TariqTalents.Schism);

    /// <summary>The spender a charge should go to on this pull: Hammer Storm with Schism talented or on a trash pull, Skull Crusher on a boss pull without it.</summary>
    public int PreferredSpenderId =>
        SchismTalented || Pull.Targets == PullKind.Multi ? HammerStormId : SkullCrusherId;

    /// <summary>Charges every buff carried, counting each Leap Smash proc that added to a buff already up.</summary>
    public int ChargesGranted => Evaluated.Sum(window => window.ChargesGranted);

    /// <summary>Charges a spender took.</summary>
    public int ChargesConsumed => Evaluated.Sum(window => window.ChargesConsumed);

    /// <summary>Charges still on a buff when it fell off, leaving out the buff a pull end clipped.</summary>
    public int ChargesWasted => Evaluated.Sum(window => window.ChargesWasted);

    /// <summary>Charges a Focused Wrath cast granted; each cast grants <see cref="StacksPerCast"/>.</summary>
    public int ChargesFromCast => Evaluated.Count(window => window.FromCast) * StacksPerCast;

    /// <summary>Charges a Leap Smash proc granted, one at a time.</summary>
    public int ChargesFromProc => ChargesGranted - ChargesFromCast;

    /// <summary>Charges that went to <see cref="PreferredSpenderId"/>.</summary>
    public int CorrectSpenders => CountChoice(SpenderChoice.Correct);

    /// <summary>Charges that went to the other spender.</summary>
    public int WrongSpenders => CountChoice(SpenderChoice.Wrong);

    /// <summary>Charges with no spender cast to name them, so their choice carries no rating.</summary>
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
    private void OnChargeRemoved(RemoveBuffStackEvent @event)
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

/// <summary>Whether a Focused Wrath charge went to the spender its pull calls for.</summary>
public enum SpenderChoice
{
    /// <summary>No spender cast could be matched to the charge, so there is nothing to rate.</summary>
    Unrated,

    /// <summary>The charge went to the spender the pull calls for.</summary>
    Correct,

    /// <summary>The charge went to the other spender.</summary>
    Wrong,
}

/// <summary>One charge leaving a Focused Wrath buff, and the spender it went to.</summary>
public sealed record FocusedWrathConsumption
{
    /// <summary>When the spender cast went out, if one was matched to the charge.</summary>
    public int? Timestamp { get; init; }

    /// <summary>The spender cast that took the charge, or <c>null</c> when no Hammer Storm or Skull Crusher cast inside the buff could be matched to it.</summary>
    public int? SpenderSpellId { get; init; }

    /// <summary>Whether the charge went to the spender the pull calls for.</summary>
    public required SpenderChoice Choice { get; init; }
}

/// <summary>One Focused Wrath buff, from the application that opened it to the removal that closed it.</summary>
public sealed record FocusedWrathWindow
{
    public required int OpenedAt { get; init; }

    public required int ClosedAt { get; init; }

    public int DurationMs => ClosedAt - OpenedAt;

    public required bool ClippedByPullEnd { get; init; }

    /// <summary>The buff opened on a Focused Wrath cast rather than on a Leap Smash proc (<c>Leap Smash.Talent.ProcCostReductionBuff</c>). A cast opens it with <see cref="FocusedWrathAnalyzer.StacksPerCast"/> charges and a proc with one.</summary>
    public required bool FromCast { get; init; }

    /// <summary>Charges the buff carried: the ones it opened with, plus one for every Leap Smash proc that added to it while it was up, up to <see cref="FocusedWrathAnalyzer.MaxStackLimit"/> at a time.</summary>
    public required int ChargesGranted { get; init; }

    /// <summary>The spender this buff's pull calls for.</summary>
    public required int PreferredSpenderId { get; init; }

    /// <summary>The charges a spender took, in the order they left the buff.</summary>
    public IReadOnlyList<FocusedWrathConsumption> Consumptions { get; init; } = [];

    public int ChargesConsumed => Consumptions.Count;

    /// <summary>Charges still on the buff when it fell off. Zero on a buff the pull end clipped, where what was left on it is an inference.</summary>
    public int ChargesWasted => ClippedByPullEnd ? 0 : ChargesGranted - ChargesConsumed;

    /// <summary>Charges that went to <see cref="PreferredSpenderId"/>.</summary>
    public int CorrectSpenders => Consumptions.Count(consumption => consumption.Choice == SpenderChoice.Correct);

    /// <summary>Charges that went to the other spender.</summary>
    public int WrongSpenders => Consumptions.Count(consumption => consumption.Choice == SpenderChoice.Wrong);

    /// <summary>Charges a spender cast was matched to, so their choice carries a rating.</summary>
    public int RatedSpenders => CorrectSpenders + WrongSpenders;
}
