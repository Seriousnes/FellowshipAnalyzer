using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[Dependency<Combatants>]
public sealed partial class RekindlingFlamesAnalyzer : Analyzer
{
    public int QualifyingDeaths { get; private set; }
    /// <summary>Engulfing Flames cooldown progress from enemy deaths, in milliseconds.</summary>
    public CooldownReductionResult CooldownReduction { get; private set; } = new();

    [On<DeathEvent>]
    private void OnDeath(DeathEvent e)
    {
        var windows = Combatants.AuraInstanceCount(e.TargetId, e.TargetInstance, Spells.EngulfingFlamesDot, e.Timestamp);
        if (windows == 0)
            return;

        var perWindowMs = (int)((Spells.EngulfingFlames.CooldownReductionOnTargetDeath ?? 0) * 1000);
        var requestedMs = windows * perWindowMs;
        var reduction = Owner.SpellUsable!.ReduceCooldown(Spells.EngulfingFlames.Id, requestedMs, e.Timestamp);

        QualifyingDeaths++;
        CooldownReduction += reduction;
    }
}
