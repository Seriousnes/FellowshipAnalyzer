using FellowshipAnalyzer.Core.Common.Spells.Sylvie;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

public static class SylvieKit
{
    public static Dictionary<int, int> ManaCosts { get; } = new Dictionary<int, int>
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

    public const int LifePetalCharges = 2;

    public const int PinkFlutterflies = 4;

    public const int RestoreLifeFlutterflies = 2;

    public static int ManaCost(int spellId) => ManaCosts.GetValueOrDefault(spellId);
}
