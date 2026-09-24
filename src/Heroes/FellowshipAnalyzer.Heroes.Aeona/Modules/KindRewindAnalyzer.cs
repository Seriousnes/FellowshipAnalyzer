using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Kind Rewind.</summary>
public interface IKindRewindAnalyzer : IAnalyzerSurface;

/// <summary>One Time Shard cast under Kind Rewind.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Target">The enemy the cast named.</param>
/// <param name="EchoesOfRuinActive">Whether the player's Echoes of Ruin was active on that enemy at the cast.</param>
/// <param name="CooldownReducedMs">Temporal Barrage cooldown the cast removed.</param>
/// <param name="CooldownReductionWastedMs">Reduction the cast granted while Temporal Barrage was already available.</param>
public sealed record KindRewindCast(
    int Timestamp,
    UnitKey Target,
    bool EchoesOfRuinActive,
    int CooldownReducedMs,
    int CooldownReductionWastedMs);

/// <summary>
/// Kind Rewind over one pull: each Time Shard, whether its target had Echoes of Ruin, and the Temporal
/// Barrage cooldown it reduced.
/// </summary>
/// <remarks>
/// The reduction is applied to <see cref="SpellUsable"/> at the cast, so Temporal Barrage's
/// availability elsewhere in the analysis follows the talent. Codex <c>talent 613</c> states 2 seconds.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(AeonaTalents.KindRewind)]
[Dependency<SpellUsable>]
public sealed partial class KindRewindAnalyzer : Analyzer, IKindRewindAnalyzer
{
    /// <summary>The Temporal Barrage cooldown one qualifying Time Shard removes.</summary>
    public const int TemporalBarrageReductionMs = 2_000;

    private readonly List<KindRewindCast> _casts = [];
    private readonly Dictionary<UnitKey, int> _echoesOpen = [];

    /// <summary>Every Time Shard cast in the pull, in cast order.</summary>
    public IReadOnlyList<KindRewindCast> Casts => _casts;

    /// <summary>Time Shard casts in the pull.</summary>
    public int CastCount => _casts.Count;

    /// <summary>Casts into an enemy with Echoes of Ruin active.</summary>
    public int CastsWithEchoesOfRuin => _casts.Count(cast => cast.EchoesOfRuinActive);

    /// <summary>Casts into an enemy without Echoes of Ruin.</summary>
    public int CastsMissed => _casts.Count(cast => !cast.EchoesOfRuinActive);

    /// <summary>Missed casts as a share (0-1) of every cast, or null with no cast.</summary>
    public double? MissedShare => _casts.Count == 0 ? null : (double)CastsMissed / _casts.Count;

    /// <summary>Temporal Barrage cooldown removed across the pull.</summary>
    public int CooldownReducedMs => _casts.Sum(cast => cast.CooldownReducedMs);

    /// <summary>Reduction granted while Temporal Barrage was already available, across the pull.</summary>
    public int CooldownReductionWastedMs => _casts.Sum(cast => cast.CooldownReductionWastedMs);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfRuinDot))]
    private void OnEchoesApplied(ApplyDebuffEvent e) => _echoesOpen[AuraWindowLedger.KeyOf(e)] = e.Timestamp;

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfRuinDot))]
    private void OnEchoesRefreshed(RefreshDebuffEvent e) => _echoesOpen[AuraWindowLedger.KeyOf(e)] = e.Timestamp;

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EchoesOfRuinDot))]
    private void OnEchoesRemoved(RemoveDebuffEvent e) => _echoesOpen.Remove(AuraWindowLedger.KeyOf(e));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.TimeShard))]
    private void OnTimeShardCast(CastEvent e)
    {
        if (e.Activation) return;

        var target = new UnitKey(e.TargetId, e.TargetInstance ?? 0);
        var active = _echoesOpen.ContainsKey(target);

        if (!active)
        {
            _casts.Add(new KindRewindCast(e.Timestamp, target, false, 0, 0));
            return;
        }

        var reduction = SpellUsable.ReduceCooldown(Spells.TemporalBarrage.FSLID, TemporalBarrageReductionMs, e.Timestamp);
        _casts.Add(new KindRewindCast(e.Timestamp, target, true, reduction.Effective, reduction.Wasted));
    }
}
