using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Measures Shield Mastery, which shortens Shield Throw and Shields Up every time Helena is hit, by an
/// amount that rides on how much Toughness she is holding. A hit taken at maximum Toughness is worth
/// ten times one taken at a tenth of it, so the talent returns most while Toughness is held high - the
/// same thing every other part of her kit is trying to do.
/// <para>
/// Seconds here are model-derived, not measured: the log records no cooldown-reduction event, so
/// whether a reduction landed is only ever what the cooldown model believed about that ability at the
/// time. Reduction generated against an ability that was already available is the waste this surfaces.
/// </para>
/// <para>
/// The talent's Season 3 constants give a factor and nothing to multiply it by, so the per-hit
/// reduction is modelled on the owner's reading as the factor times Toughness as a share of its
/// maximum, times the ability's base cooldown. No log held locally has a player running the talent, so
/// the shape has not been checked against live data.
/// </para>
/// </summary>
[RequiresTalent(HelenaTalents.ShieldMastery)]
[Dependency<SpellUsable>]
public sealed partial class ShieldMasteryAnalyzer : Analyzer
{
    private readonly Dictionary<int, Contribution> _contributions = [];
    private readonly Dictionary<int, int> _baseCooldownMs = [];

    private int _hitsTaken;
    private int _hitsWithoutToughness;

    /// <summary>
    /// The <c>Toughness.Talent.BouncyProjectileDefensiveBuffCooldownReduction.Factor</c> value: the
    /// share of an ability's base cooldown one hit taken at full Toughness returns.
    /// </summary>
    public const double ToughnessFactor = 0.1;

    /// <summary>The abilities the talent shortens, named for the constant's own <c>BouncyProjectile</c> and <c>DefensiveBuff</c>.</summary>
    public static IReadOnlyList<int> Targets { get; } = [Spells.ShieldThrow.FSLID, Spells.ShieldsUp.FSLID];

    /// <summary>Each shortened ability's totals, ordered by the seconds it wasted.</summary>
    public IReadOnlyList<ShieldMasteryContribution> ByTarget => Result.ByTarget;

    /// <summary>Reduction generated this pull, in milliseconds.</summary>
    public int GeneratedMs => Result.GeneratedMs;

    /// <summary>Reduction that shortened a running cooldown, in milliseconds.</summary>
    public int AppliedMs => Result.AppliedMs;

    /// <summary>Reduction generated against an ability that was already available, in milliseconds.</summary>
    public int WastedMs => Result.GeneratedMs - Result.AppliedMs;

    /// <summary>Share (0-1) of generated reduction that landed on a running cooldown.</summary>
    public double Efficiency => Result.GeneratedMs > 0 ? (double)Result.AppliedMs / Result.GeneratedMs : 0;

    /// <summary>Hits Helena took this pull that carried a Toughness reading to size the reduction from.</summary>
    public int HitsTaken => _hitsTaken;

    /// <summary>
    /// Hits that carried no Toughness reading, and so generated nothing here. Read it as the share of
    /// the talent's real output these figures could not see.
    /// </summary>
    public int HitsWithoutToughness => _hitsWithoutToughness;

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent)
    {
        if (FindToughness(damageEvent.TargetResources) is not { Max: > 0 } toughness)
        {
            _hitsWithoutToughness++;
            return;
        }

        _hitsTaken++;

        var share = Math.Clamp(toughness.Amount / (double)toughness.Max, 0, 1);
        if (share <= 0) return;

        foreach (var target in Targets)
        {
            var requested = (int)Math.Round(ToughnessFactor * share * BaseCooldownMs(target));
            if (requested <= 0) continue;

            var reduction = SpellUsable.ReduceCooldown(target, requested, damageEvent.Timestamp);

            if (!_contributions.TryGetValue(target, out var contribution))
                _contributions[target] = contribution = new Contribution();

            contribution.Generated += reduction.GeneratedMs;
            contribution.Applied += reduction.AppliedMs;
            contribution.Events++;
        }
    }

    private int BaseCooldownMs(int spellId)
    {
        if (_baseCooldownMs.TryGetValue(spellId, out var cached)) return cached;

        var seconds = Owner.GetModule<Abilities>()?.GetExpectedCooldown(spellId) ?? 0;
        return _baseCooldownMs[spellId] = (int)(seconds * 1000);
    }

    private static ClassResource? FindToughness(ActorResources? resources)
    {
        if (resources?.Resources is not { Count: > 0 } list) return null;

        foreach (var resource in list)
            if (resource.Type == ResourceTypes.Secondary)
                return resource;

        return null;
    }

    private Computed Result => field ??= Compute();

    private Computed Compute()
    {
        var targets = new List<ShieldMasteryContribution>(_contributions.Count);
        var generated = 0;
        var applied = 0;

        foreach (var (target, contribution) in _contributions)
        {
            targets.Add(new ShieldMasteryContribution(
                target, contribution.Events, contribution.Generated, contribution.Applied));

            generated += contribution.Generated;
            applied += contribution.Applied;
        }

        targets.Sort(static (left, right) => right.WastedMs.CompareTo(left.WastedMs));

        return new Computed(targets, generated, applied);
    }

    private sealed class Contribution
    {
        public int Generated { get; set; }
        public int Applied { get; set; }
        public int Events { get; set; }
    }

    private sealed record Computed(IReadOnlyList<ShieldMasteryContribution> ByTarget, int GeneratedMs, int AppliedMs);
}

/// <summary>
/// How much reduction Shield Mastery aimed at one ability, and how much of it landed.
/// </summary>
/// <param name="TargetSpellId">The ability whose cooldown was shortened.</param>
/// <param name="Events">Hits taken that generated reduction on this ability.</param>
/// <param name="GeneratedMs">Reduction generated, after Ability Cooldown Reduction scaling.</param>
/// <param name="AppliedMs">The share of <paramref name="GeneratedMs"/> that shortened a running cooldown.</param>
public sealed record ShieldMasteryContribution(
    int TargetSpellId,
    int Events,
    int GeneratedMs,
    int AppliedMs)
{
    /// <summary>Reduction generated while the ability was already available.</summary>
    public int WastedMs => GeneratedMs - AppliedMs;

    /// <summary>Share (0-1) of the generated reduction that landed.</summary>
    public double Efficiency => GeneratedMs > 0 ? (double)AppliedMs / GeneratedMs : 0;
}
