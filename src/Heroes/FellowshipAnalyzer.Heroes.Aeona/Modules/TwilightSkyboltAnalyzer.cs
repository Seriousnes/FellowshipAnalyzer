using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Twilight Skybolt's charge state at one instant inside a pull, either the state the pull opened on
/// or the state a <see cref="UpdateSpellUsableEvent"/> reported.
/// </summary>
/// <param name="Timestamp">The instant this state applies from.</param>
/// <param name="ChargesAvailable">Charges castable from this instant until the next change.</param>
public readonly record struct SkyboltChargeSample(int Timestamp, int ChargesAvailable);

/// <summary>
/// Twilight Skybolt's charge economy over one pull: the casts, the charges held across the pull, and
/// the time the pull spent at the maximum charge count. Charge state is reconstructed by
/// <see cref="SpellUsable"/> from the spellbook's cooldown and charge count, so it depends on Twilight
/// Skybolt being registered in <see cref="Abilities.Spellbook"/>.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class TwilightSkyboltAnalyzer : Analyzer
{
    private readonly List<int> _casts = [];
    private readonly List<SkyboltChargeSample> _samples = [];

    private int? _chargesAtPullStart;
    private int? _timeAtMaxChargesMs;

    /// <summary>When each Twilight Skybolt cast completed, in cast order.</summary>
    public IReadOnlyList<int> Casts => _casts;

    /// <summary>How many times Twilight Skybolt was cast during the pull.</summary>
    public int CastCount => _casts.Count;

    /// <summary>The charge count Twilight Skybolt can bank, read from the spellbook.</summary>
    public int MaxCharges => CoreItems.TwilightSkybolt.Charges;

    /// <summary>
    /// Every charge state inside the pull, chronological, opening on the state the pull started with.
    /// </summary>
    public IReadOnlyList<SkyboltChargeSample> ChargeSamples => Timeline;

    /// <summary>Milliseconds of the pull spent holding every charge.</summary>
    public int TimeAtMaxChargesMs => _timeAtMaxChargesMs ??= ComputeTimeAtMaxCharges();

    /// <summary>The share of the pull spent holding every charge, 0 to 1.</summary>
    public double TimeAtMaxChargesShare =>
        PullDurationMs <= 0 ? 0 : (double)TimeAtMaxChargesMs / PullDurationMs;

    /// <summary>
    /// Recharges the pull had room for while every charge was already available, as the time at the
    /// maximum divided by one charge's recharge duration. Reads 0 without a recharge duration.
    /// </summary>
    public int ChargesLost => RechargeDurationMs <= 0 ? 0 : TimeAtMaxChargesMs / RechargeDurationMs;

    /// <summary>
    /// How long one Twilight Skybolt charge takes to recharge under the player's haste, from
    /// <see cref="SpellUsable.RechargeDuration"/>, falling back to the spellbook cooldown when the
    /// cooldown tracker has nothing for it.
    /// </summary>
    public int RechargeDurationMs
    {
        get
        {
            var tracked = Owner.SpellUsable?.RechargeDuration(CoreItems.TwilightSkybolt.FSLID) ?? 0;
            if (tracked > 0)
                return tracked;

            return CoreItems.TwilightSkybolt.Cooldown is { } cooldown and > 0 ? (int)(cooldown * 1000) : 0;
        }
    }

    /// <summary>The pull's length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    private List<SkyboltChargeSample> Timeline => field ??= BuildTimeline();

    [On<PullStartEvent>]
    private void OnPullStart() =>
        _chargesAtPullStart = Owner.SpellUsable?.ChargesAvailable(CoreItems.TwilightSkybolt.FSLID);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(CoreItems.TwilightSkybolt))]
    private void OnCast(CastEvent e) => _casts.Add(e.Timestamp);

    [On<UpdateSpellUsableEvent>(Spell = nameof(CoreItems.TwilightSkybolt))]
    private void OnUsable(UpdateSpellUsableEvent e) =>
        _samples.Add(new SkyboltChargeSample(e.Timestamp, e.ChargesAvailable));

    private List<SkyboltChargeSample> BuildTimeline()
    {
        var start = Pull.StartTime;
        var end = Pull.EndTime;

        var timeline = new List<SkyboltChargeSample>(_samples.Count + 1)
        {
            new(start, _chargesAtPullStart ?? MaxCharges),
        };

        foreach (var sample in _samples)
        {
            if (sample.Timestamp > end)
                break;

            timeline.Add(new SkyboltChargeSample(Math.Max(sample.Timestamp, start), sample.ChargesAvailable));
        }

        return timeline;
    }

    private int ComputeTimeAtMaxCharges()
    {
        var end = Pull.EndTime;
        var maxCharges = MaxCharges;
        var timeline = Timeline;

        var atMax = 0;
        for (var i = 0; i < timeline.Count; i++)
        {
            var segmentStart = timeline[i].Timestamp;
            var segmentEnd = i + 1 < timeline.Count ? timeline[i + 1].Timestamp : end;

            if (segmentEnd > segmentStart && timeline[i].ChargesAvailable >= maxCharges)
                atMax += segmentEnd - segmentStart;
        }

        return atMax;
    }
}
