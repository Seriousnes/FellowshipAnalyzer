using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Game;

using SpellRegistry = FellowshipAnalyzer.Core.Common.Spells.SpellRegistry;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

public static class SylvieKit
{
    public const double EmbraceManaCostScaler = 0.8;

    public const int PricklyVineDurationMs = 27_000;

    public const int PricklyVineSpawnMs = 1_000;

    public const double FlutterflyHealPerVine = 0.03;

    public const double SafeHavenHealingTakenMultiplier = 1.4;

    public const int SafeHavenDurationMs = 15_000;

    public const double BlueyAllyHealScaler = 2.0;

    public const double BlueySelfHealScaler = 1.5;

    public const double HeartBloomAccumulatedShare = 0.12;

    public static readonly int HeartBloomTickIntervalMs = (int)(SpellTuning.HeartBloom.TickInterval * 1000);

    public static readonly int HeartBloomDurationMs = (int)(SpellTuning.HeartBloom.Duration * 1000);

    /// <summary>Heart Bloom releases in one cast at Haste 0.</summary>
    public static int HeartBloomReleases => HeartBloomDurationMs / HeartBloomTickIntervalMs;

    public static readonly int NettleboltCooldownReductionMs = (int)(SpellTuning.NettleboltCooldownReduction.CooldownReductionInSecondsDamage * 1000);

    public static readonly int NettleboltCritCooldownReductionMs = (int)(SpellTuning.NettleboltCooldownReduction.CooldownReductionInSecondsCrit * 1000);

    public static int LifePetalCharges => Spells.LifePetal.Charges;

    public const int PinkFlutterflies = 4;

    public const int RestoreLifeFlutterflies = 2;

    /// <summary>The listed mana cost of <paramref name="spellId"/> before the Fluttercall: Embrace discount; 0 for a spell that spends no mana.</summary>
    public static int ManaCost(int spellId) => SpellRegistry.MaybeGet(spellId)?.Cost(ResourceTypes.Mana) ?? 0;
}
