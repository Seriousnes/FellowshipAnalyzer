using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;
using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[RequiresTalent(ArdeosTalents.RollingFlames)]
public sealed partial class RollingFlamesAnalyzer : Analyzer
{
    private const int SEARING_BLAZE_CDR = 250;
    private const int INFERNAL_WAVE_CDR = 1000;

    private readonly Dictionary<int, SpellCdrEffectiveness> spellCdr = new()
    {
        { Spells.SearingBlaze.Id, new() },
        { Spells.InfernalWave.Id, new() },
    };

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    public void OnSearingBlazeDamage(DamageEvent _)
    {
        var cdr = Owner.SpellUsable!.ReduceCooldown(Spells.EngulfingFlames.Id, SEARING_BLAZE_CDR);
        spellCdr[Spells.SearingBlaze.Id].Total += cdr.GeneratedMs;
        spellCdr[Spells.SearingBlaze.Id].Applied += cdr.AppliedMs;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.InfernalWave))]
    public void OnInfernalWaveCast(CastEvent _)
    {
        var cdr = Owner.SpellUsable!.ReduceCooldown(Spells.EngulfingFlames.Id, INFERNAL_WAVE_CDR);
        spellCdr[Spells.InfernalWave.Id].Total += cdr.GeneratedMs;
        spellCdr[Spells.InfernalWave.Id].Applied += cdr.AppliedMs;
    }

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

public sealed record RollingFlamesCdr(Spell Spell, int GeneratedMs, int EffectiveMs)
{
    public int WastedMs => GeneratedMs - EffectiveMs;

    public double Efficiency => GeneratedMs == 0 ? 0d : (double)EffectiveMs / GeneratedMs;
}

internal record SpellCdrEffectiveness
{
    public int Total { get; set; }
    public int Applied { get; set; }
}
