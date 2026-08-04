using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public partial class CarnageAnalyzer : Analyzer
{
    private const int debounceMs = 100;
    public const int CooldownReductionMs = 1000;
    private int _lastSpin = 0;

    private readonly List<Core.Common.Spells.Spell> affectedSpells = [
        Spells.ReaverEdge,
        Spells.BloodArc,
        Spells.HeartSplitter,
        Spells.Rupture
    ];

    private readonly Dictionary<int, CooldownReductionResult> cooldownReductionBySpell = new()
    {
        { Spells.ReaverEdge.Id, new() },
        { Spells.BloodArc.Id, new() },
        { Spells.HeartSplitter.Id, new() },
        { Spells.Rupture.Id, new()  }
    };

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.GrimCarve))]
    public void OnSpin(DamageEvent damageEvent)
    {
        if (damageEvent.Timestamp <= _lastSpin + debounceMs) return;

        _lastSpin = damageEvent.Timestamp;

        foreach (var spell in affectedSpells)
        {            
            var result = SpellUsable.ReduceCooldown(spell.Id, CooldownReductionMs);
            cooldownReductionBySpell[spell.Id] += result;
        }
    }
}
