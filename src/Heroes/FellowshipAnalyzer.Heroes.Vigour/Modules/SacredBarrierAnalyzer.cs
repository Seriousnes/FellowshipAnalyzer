using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

using VigourTalents = FellowshipAnalyzer.Core.Common.Spells.VigourTalents;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

[RequiresTalent(VigourTalents.SacredBarrier)]
[Before<SpellUsable>]
[Dependency<StatTracker>]
public sealed partial class SacredBarrierAnalyzer : Analyzer
{
    private static readonly CooldownScope LuminousBarrierScope = new Core.Common.Spells.Spell[] { Spells.LuminousBarrier };

    private CooldownModifier? _current;

    [On<ChangeHasteEvent>(By = Actor.Player)]
    private void OnHasteChanged(ChangeHasteEvent e)
    {
        var next = new CooldownModifier(e.NewHaste ?? 0, LuminousBarrierScope);
        if (_current == next) return;

        if (_current is not null) StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, _current, e);
        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, next, e);
        _current = next;
    }
}
