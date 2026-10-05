using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

[Dependency<SpellUsable>]
public sealed partial class AvatarOfLightAnalyzer : Analyzer
{
    public static bool HasNoCooldown(int spellId) =>
        spellId == Spells.RadiantBlast.FSLID || spellId == Spells.CircleOfLight.FSLID;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AvatarOfLightBuff))]
    private void OnApplied(ApplyBuffEvent e)
    {
        SpellUsable.EndCooldown(Spells.RadiantBlast.FSLID, e.Timestamp, restoreAllCharges: true);
        SpellUsable.EndCooldown(Spells.CircleOfLight.FSLID, e.Timestamp, restoreAllCharges: true);
    }
}
