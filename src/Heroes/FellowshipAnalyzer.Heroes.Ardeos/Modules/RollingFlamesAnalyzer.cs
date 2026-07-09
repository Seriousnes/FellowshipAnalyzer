using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Ardeos.Statistics;

using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[ActiveWhen<RollingFlamesActive>]
public sealed partial class RollingFlamesAnalyzer : Analyzer
{
    private const int SEARING_BLAZE_CDR = 250;
    private const int INFERNAL_WAVE_CDR = 1000;

    private readonly Dictionary<int, SpellCdrEffectiveness> spellCdr = new()
    {
        { Spells.SearingBlaze.Id, new() },
        { Spells.InfernalWave.Id, new() },
    };

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlaze))]
    public void OnSearingBlazeTick(DamageEvent _)
    {
        var appliedCdr = Owner.SpellUsable!.ReduceCooldown(Spells.SearingBlaze.Id, SEARING_BLAZE_CDR);
        spellCdr[Spells.SearingBlaze.Id].Total += SEARING_BLAZE_CDR;
        spellCdr[Spells.SearingBlaze.Id].Applied += appliedCdr;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.InfernalWave))]
    public void OnInfernalWaveTick(DamageEvent _)
    {
        var appliedCdr = Owner.SpellUsable!.ReduceCooldown(Spells.InfernalWave.Id, INFERNAL_WAVE_CDR);
        spellCdr[Spells.InfernalWave.Id].Total += INFERNAL_WAVE_CDR;
        spellCdr[Spells.InfernalWave.Id].Applied += appliedCdr;
    }

    public override Type? StatisticsComponentType => typeof(RollingFlamesStatistics);

    /// <summary>
    /// Per-spell cooldown reduction Rolling Flames drove over the encounter: the total CDR each
    /// source spell's damage generated and the portion that actually shortened a running cooldown.
    /// </summary>
    public IReadOnlyList<RollingFlamesCdr> CooldownReductions =>
    [
        ConvertToRollingFlamesCdr(Spells.SearingBlaze),
        ConvertToRollingFlamesCdr(Spells.InfernalWave),
    ];

    private RollingFlamesCdr ConvertToRollingFlamesCdr(Spell spell)
    {
        var cdr = spellCdr[spell.Id];
        return new RollingFlamesCdr(spell, cdr.Total, cdr.Applied);
    }
}

/// <summary>
/// Per-spell projection of Rolling Flames cooldown reduction for the statistics readout.
/// </summary>
/// <param name="Spell">The spell whose damage drove the reduction.</param>
/// <param name="GeneratedMs">Total cooldown reduction generated, in milliseconds.</param>
/// <param name="EffectiveMs">Reduction that actually shortened a running cooldown, in milliseconds.</param>
public sealed record RollingFlamesCdr(Spell Spell, int GeneratedMs, int EffectiveMs)
{
    /// <summary>Reduction generated while the spell was already off cooldown, in milliseconds.</summary>
    public int WastedMs => GeneratedMs - EffectiveMs;

    /// <summary>Share of the generated reduction that shortened a running cooldown (0 when nothing generated).</summary>
    public double Efficiency => GeneratedMs == 0 ? 0d : (double)EffectiveMs / GeneratedMs;
}

internal record SpellCdrEffectiveness
{
    public int Total { get; set; }
    public int Applied { get; set; }
}

internal class RollingFlamesActive : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => context.SelectedCombatant.HasTalent(talentId: 226);
}
