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

    private readonly Dictionary<int, CooldownReductionResult> spellCdr = new()
    {
        { Spells.SearingBlaze.Id, new() },
        { Spells.InfernalWave.Id, new() },
    };

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    public void OnSearingBlazeDamage(DamageEvent _) =>
        spellCdr[Spells.SearingBlaze.Id] +=
            Owner.SpellUsable!.ReduceCooldown(Spells.EngulfingFlames.Id, SEARING_BLAZE_CDR);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.InfernalWave))]
    public void OnInfernalWaveCast(CastEvent _) =>
        spellCdr[Spells.InfernalWave.Id] +=
            Owner.SpellUsable!.ReduceCooldown(Spells.EngulfingFlames.Id, INFERNAL_WAVE_CDR);

    public IReadOnlyList<RollingFlamesCdr> CooldownReductions =>
    [
        new(Spells.SearingBlaze, spellCdr[Spells.SearingBlaze.Id]),
        new(Spells.InfernalWave, spellCdr[Spells.InfernalWave.Id]),
    ];
}

public sealed record RollingFlamesCdr(Spell Spell, CooldownReductionResult CooldownReduction);
