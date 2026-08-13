using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Running totals of the selected player's output over the dungeon: damage dealt, effective healing done, and
/// damage consumed by their absorption shields. Statistics that report what one ability, item, or gem
/// contributed read <see cref="ShareOfDamage"/> / <see cref="ShareOfHealing"/> to express that contribution
/// as a fraction of everything the player did.
/// <para>
/// Totals count the selected player only, so a share computed against them is only meaningful for a
/// numerator gathered the same way.
/// </para>
/// </summary>
public sealed partial class ThroughputTracker : Analyzer
{
    /// <summary>Total damage the player dealt over the dungeon.</summary>
    public long TotalDamage { get; private set; }

    /// <summary>Total effective healing the player did over the dungeon, excluding overheal.</summary>
    public long TotalHealing { get; private set; }

    /// <summary>Total healing the player lost to overheal.</summary>
    public long TotalOverheal { get; private set; }

    /// <summary>Total damage the player's absorption shields consumed.</summary>
    public long TotalAbsorbed { get; private set; }

    /// <summary>The analyzed dungeon's duration in milliseconds.</summary>
    public int DungeonDurationMs => Owner.DungeonDurationMs;

    /// <summary>Damage per second across the dungeon.</summary>
    public double DamagePerSecond => PerSecond(TotalDamage);

    /// <summary>Effective healing per second across the dungeon.</summary>
    public double HealingPerSecond => PerSecond(TotalHealing);

    /// <summary>Absorbed damage per second across the dungeon.</summary>
    public double AbsorbedPerSecond => PerSecond(TotalAbsorbed);

    /// <summary>Converts an amount accumulated over the dungeon into a per-second rate.</summary>
    public double PerSecond(long amount) =>
        DungeonDurationMs > 0 ? amount / (DungeonDurationMs / 1000d) : 0;

    /// <summary><paramref name="amount"/> as a fraction of <see cref="TotalDamage"/>, or zero when no damage was dealt.</summary>
    public double ShareOfDamage(long amount) => TotalDamage > 0 ? (double)amount / TotalDamage : 0;

    /// <summary><paramref name="amount"/> as a fraction of <see cref="TotalHealing"/>, or zero when no healing was done.</summary>
    public double ShareOfHealing(long amount) => TotalHealing > 0 ? (double)amount / TotalHealing : 0;

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent) => TotalDamage += damageEvent.Amount;

    [On<HealEvent>(By = Actor.Player)]
    private void OnHeal(HealEvent healEvent)
    {
        TotalHealing += healEvent.Amount;
        TotalOverheal += healEvent.Overheal ?? 0;
    }

    [On<AbsorbedEvent>(To = Actor.Player)]
    private void OnAbsorbed(AbsorbedEvent absorbedEvent) => TotalAbsorbed += absorbedEvent.Amount;
}
