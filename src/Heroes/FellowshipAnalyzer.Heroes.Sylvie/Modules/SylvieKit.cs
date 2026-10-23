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

    public const int HeartBloomTickIntervalMs = 2_000;

    public const int HeartBloomDurationMs = 16_000;

    public static int HeartBloomReleases => HeartBloomDurationMs / HeartBloomTickIntervalMs;

    public const int NettleboltCooldownReductionMs = 1_500;

    public const int NettleboltCritCooldownReductionMs = 3_000;

    public static int LifePetalCharges => Spells.LifePetal.Charges;

    public const int PinkFlutterflies = 4;

    public const int RestoreLifeFlutterflies = 2;

    /// <summary>The listed mana cost of <paramref name="spellId"/> before the Fluttercall: Embrace discount; 0 for a spell that spends no mana.</summary>
    public static int ManaCost(int spellId) => SpellRegistry.MaybeGet(spellId)?.Cost(ResourceTypes.Mana) ?? 0;
}
