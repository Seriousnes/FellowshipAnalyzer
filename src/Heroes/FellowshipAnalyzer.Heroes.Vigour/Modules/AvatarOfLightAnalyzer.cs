using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

[After<SpellUsable>]
[Dependency<SpellUsable>]
public sealed partial class AvatarOfLightAnalyzer : Analyzer
{
    private bool _active;

    public int CooldownsReset { get; private set; }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AvatarOfLightBuff))]
    private void OnApplied(ApplyBuffEvent e)
    {
        _active = true;
        SpellUsable.EndCooldown(Spells.RadiantBlast.FSLID, e.Timestamp, restoreAllCharges: true);
        SpellUsable.EndCooldown(Spells.CircleOfLight.FSLID, e.Timestamp, restoreAllCharges: true);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AvatarOfLightBuff))]
    private void OnRemoved() => _active = false;

    [On<CastEvent>(By = Actor.Player, Spells = new[] { nameof(Spells.RadiantBlast), nameof(Spells.CircleOfLight) })]
    private void OnEnhancedCast(CastEvent e)
    {
        if (!_active || e.Fake) return;

        SpellUsable.EndCooldown(e.Ability.Id, e.Timestamp);
        CooldownsReset++;
    }
}
