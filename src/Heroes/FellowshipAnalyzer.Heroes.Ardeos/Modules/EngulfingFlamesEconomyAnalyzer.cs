using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Measures Ardeos's Engulfing Flames charge economy. Engulfing Flames is a flat two-charge,
/// twenty-second-recharge spell; the goal is to enter each Wildfire burn window holding both charges
/// so it can be double-cast inside the burst. Two waste signals are surfaced: window readiness (how
/// many Engulfing Flames charges were available at each Wildfire cast, two being ready) and overcap
/// (Engulfing Flames sitting at max charges long enough that full recharge periods were wasted).
/// </summary>
/// <remarks>
/// Readiness is sampled live from <see cref="SpellUsable"/> at each Wildfire cast. Overcap is
/// reconstructed from the charge count over time: <see cref="SpellUsable"/> fabricates an
/// <see cref="UpdateSpellUsableEvent"/> whenever Engulfing Flames spends or restores a charge, and
/// the intervals it spends at max charges, assuming it starts the pull at max, are summed and divided
/// by the recharge period, so brief holds before an imminent window cost nothing while sitting capped
/// across recharge cycles is charged as waste. One analyzer serves single-target and AoE pulls, since
/// the burn window is identical for both.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class EngulfingFlamesEconomyAnalyzer : Analyzer
{
    private const int FallbackRechargeMs = 20_000;

    private readonly List<WindowReadiness> _windows = [];
    private readonly List<(int Timestamp, int Charges)> _chargeSamples = [];

    /// <summary>Per-Wildfire-cast snapshots of Engulfing Flames charge availability.</summary>
    public IReadOnlyList<WindowReadiness> Windows => _windows;

    private long? _cappedMs;
    private long CappedMs => _cappedMs ??= ComputeCappedMilliseconds(Spells.EngulfingFlames.Charges);

    public int WindowsEvaluated => _windows.Count;
    public int WindowsReady => _windows.Count(w => w.Ready);
    public int WindowsShort => WindowsEvaluated - WindowsReady;

    /// <summary>Full Engulfing Flames recharge periods wasted sitting at maximum charges.</summary>
    public int WastedCharges
    {
        get
        {
            var rechargeMs = Spells.EngulfingFlames.Cooldown is { } cooldown and > 0
                ? (int)(cooldown * 1000)
                : FallbackRechargeMs;
            return rechargeMs > 0 ? (int)(CappedMs / rechargeMs) : 0;
        }
    }

    /// <summary>Total seconds Engulfing Flames spent at maximum charges during the pull.</summary>
    public double CappedSeconds => Math.Round(CappedMs / 1000d, 1);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Wildfire))]
    private void OnWildfireCast(CastEvent e)
    {
        var charges = Owner.SpellUsable!.ChargesAvailable(Spells.EngulfingFlames.Id);
        _windows.Add(new WindowReadiness
        {
            Timestamp = e.Timestamp,
            ChargesAvailable = charges,
            Ready = charges >= Spells.EngulfingFlames.Charges,
        });
    }

    [On<UpdateSpellUsableEvent>]
    private void OnUpdateSpellUsable(UpdateSpellUsableEvent e)
    {
        if (e.Ability.FSLID != Spells.EngulfingFlames.FSLID)
            return;

        _chargeSamples.Add((e.Timestamp, e.ChargesAvailable));
    }

    private long ComputeCappedMilliseconds(int maxCharges)
    {
        var startTime = Pull.StartTime;
        var endTime = Pull.EndTime;
        if (endTime <= startTime)
            return 0;

        var transitions = new List<(int Time, int Charges)>(_chargeSamples.Count + 1)
        {
            (startTime, maxCharges),
        };
        foreach (var (timestamp, charges) in _chargeSamples)
            transitions.Add((Math.Clamp(timestamp, startTime, endTime), charges));

        long cappedMs = 0;
        for (var i = 0; i < transitions.Count; i++)
        {
            var segmentStart = transitions[i].Time;
            var segmentEnd = i + 1 < transitions.Count ? transitions[i + 1].Time : endTime;
            if (segmentEnd > segmentStart && transitions[i].Charges >= maxCharges)
                cappedMs += segmentEnd - segmentStart;
        }

        return cappedMs;
    }

    /// <summary>
    /// Snapshot of Engulfing Flames charge availability at a single Wildfire cast.
    /// </summary>
    public sealed record WindowReadiness
    {
        public int Timestamp { get; init; }
        public int ChargesAvailable { get; init; }
        public bool Ready { get; init; }
    }
}
