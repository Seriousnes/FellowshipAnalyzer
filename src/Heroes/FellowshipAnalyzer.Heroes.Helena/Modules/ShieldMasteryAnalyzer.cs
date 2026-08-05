using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

[RequiresTalent(HelenaTalents.ShieldMastery)]
[Dependency<SpellUsable>]
public sealed partial class ShieldMasteryAnalyzer : Analyzer
{
    private readonly Dictionary<int, Contribution> _contributions = [];
    private readonly Dictionary<int, int> _baseCooldownMs = [];

    private int _hitsTaken;
    private int _hitsWithoutToughness;

    public const double ToughnessFactor = 0.1;

    public static IReadOnlyList<int> Targets { get; } = [Spells.ShieldThrow.FSLID, Spells.ShieldsUp.FSLID];

    public IReadOnlyList<ShieldMasteryContribution> ByTarget => Result.ByTarget;

    public CooldownReductionResult CooldownReduction => Result.CooldownReduction;

    public int HitsTaken => _hitsTaken;

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

            contribution.CooldownReduction += reduction;
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
        var total = new CooldownReductionResult();

        foreach (var (target, contribution) in _contributions)
        {
            targets.Add(new ShieldMasteryContribution(
                target, contribution.Events, contribution.CooldownReduction));

            total += contribution.CooldownReduction;
        }

        targets.Sort(static (left, right) =>
            right.CooldownReduction.Wasted.CompareTo(left.CooldownReduction.Wasted));

        return new Computed(targets, total);
    }

    private sealed class Contribution
    {
        public CooldownReductionResult CooldownReduction { get; set; }
        public int Events { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<ShieldMasteryContribution> ByTarget,
        CooldownReductionResult CooldownReduction);
}

public sealed record ShieldMasteryContribution(
    int TargetSpellId,
    int Events,
    CooldownReductionResult CooldownReduction);
