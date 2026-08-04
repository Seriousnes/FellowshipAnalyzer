using FellowshipAnalyzer.Core.Common.Spells.Sylvie;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// The Season 3 numbers Sylvie's analysis is measured against, taken from the <c>Constants</c> block
/// of <c>s3/hero_data.json</c>. Mana costs live here rather than on the spell registry because the
/// merge that builds <c>spelldb.json</c> maps only <c>OrbCost</c>, <c>SpiritPointCost</c> and a
/// <c>CostType</c>-keyed <c>Cost</c>, and Sylvie's costs are published as a per-ability
/// <c>ManaCost</c> that has no slot in that mapping. The logs carry no per-cast cost either, so
/// without this table every mana figure would read zero.
/// </summary>
public static class SylvieKit
{
    /// <summary>Mana each ability costs to cast, keyed by spell id (<c>Constants.&lt;Ability&gt;.ManaCost</c>).</summary>
    public static IReadOnlyDictionary<int, int> ManaCosts { get; } = new Dictionary<int, int>
    {
        [Spells.Nettlebolt.FSLID] = 5,
        [Spells.PricklyVine.FSLID] = 18,
        [Spells.HiddenTrail.FSLID] = 18,
        [Spells.FluttercallProtect.FSLID] = 18,
        [Spells.FluttercallEmbrace.FSLID] = 18,
        [Spells.FluttercallHeal.FSLID] = 39,
        [Spells.FluttercallRestoreLife.FSLID] = 61,
        [Spells.IronleafWard.FSLID] = 63,
        [Spells.EnfeeblingRootsap.FSLID] = 77,
        [Spells.LifePetal.FSLID] = 79,
        [Spells.CureAilment.FSLID] = 82,
        [Spells.SafeHaven.FSLID] = 117,
        [Spells.HeartBloom.FSLID] = 118,
    };

    /// <summary>
    /// The mana multiplier Bluey applies while parked on Sylvie herself
    /// (<c>Constants.BlueFlutterfly.SelfBuff.ManaCostScaler</c>).
    /// </summary>
    public const double EmbraceManaCostScaler = 0.8;

    /// <summary>How long one Prickly Vine lives, in milliseconds (<c>Constants.Prickly Vine.Duration</c>).</summary>
    public const int PricklyVineDurationMs = 27_000;

    /// <summary>How long a vine takes to come up before it starts firing, in milliseconds (<c>Constants.Prickly Vine.SpawnDuration</c>).</summary>
    public const int PricklyVineSpawnMs = 1_000;

    /// <summary>The flutterfly healing increase each live Prickly Vine grants (<c>Constants.Fluttercall: Heal.IncreasedHealPerVine.HealMultiplierPerVine</c>).</summary>
    public const double FlutterflyHealPerVine = 0.03;

    /// <summary>The healing-taken multiplier Safe Haven grants allies inside it (<c>Constants.Safe Haven.Buff.IncomingHealMultiplier</c>).</summary>
    public const double SafeHavenHealingTakenMultiplier = 1.4;

    /// <summary>The flutterfly healing multiplier Bluey grants an ally (<c>Constants.BlueFlutterfly.TargetBuff.HotHealScaler</c>).</summary>
    public const double BlueyAllyHealScaler = 2.0;

    /// <summary>The flutterfly healing multiplier Bluey grants Sylvie herself (<c>Constants.BlueFlutterfly.SelfBuff.HotHealScaler</c>).</summary>
    public const double BlueySelfHealScaler = 1.5;

    /// <summary>The share of accumulated healing Heart Bloom banks (<c>Constants.Heart Bloom.AccumulatedHealScalar</c>).</summary>
    public const double HeartBloomAccumulatedShare = 0.12;

    /// <summary>Heart Bloom's release interval in milliseconds (<c>Constants.Heart Bloom.TickInterval</c>).</summary>
    public const int HeartBloomTickIntervalMs = 2_000;

    /// <summary>How long Heart Bloom keeps releasing, in milliseconds (<c>Constants.Heart Bloom.Duration</c>).</summary>
    public const int HeartBloomDurationMs = 16_000;

    /// <summary>Releases one Heart Bloom makes over its duration.</summary>
    public static int HeartBloomReleases => HeartBloomDurationMs / HeartBloomTickIntervalMs;

    /// <summary>Seconds a Nettlebolt that lands takes off Life Petal (<c>Constants.Nettlebolt.HealTotemCooldownReduction.CooldownReductionInSecondsDamage</c>).</summary>
    public const int NettleboltCooldownReductionMs = 1_500;

    /// <summary>Seconds a Nettlebolt that crits takes off Life Petal (<c>Constants.Nettlebolt.HealTotemCooldownReduction.CooldownReductionInSecondsCrit</c>).</summary>
    public const int NettleboltCritCooldownReductionMs = 3_000;

    /// <summary>Life Petal's charge count (<c>Constants.Life Petal.MaxCharges</c>).</summary>
    public const int LifePetalCharges = 2;

    /// <summary>Pink Flutterflies Sylvie can bank at once, the maximum the log's tertiary resource reports.</summary>
    public const int PinkFlutterflies = 4;

    /// <summary>
    /// Pink flutterflies one <see cref="Spells.FluttercallRestoreLife"/> cast sends. The ability is
    /// <c>GA_Mosse_PinkFlutterfly_Double</c> in the game data, and the log's tertiary bank steps
    /// 4 - 2 - 0 across its casts, so a Restore Life window holds two of the four.
    /// </summary>
    public const int RestoreLifeFlutterflies = 2;

    /// <summary>The mana <paramref name="spellId"/> costs, or zero when it is not a mana-costed ability.</summary>
    public static int ManaCost(int spellId) => ManaCosts.GetValueOrDefault(spellId);
}
