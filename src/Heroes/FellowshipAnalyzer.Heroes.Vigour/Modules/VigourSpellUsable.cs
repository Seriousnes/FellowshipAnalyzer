using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public sealed partial class VigourSpellUsable(
    Lazy<CoreAbilities> abilities,
    Lazy<DebugAnnotations> debugAnnotations,
    Lazy<Haste> haste,
    Lazy<StatTracker> statTracker) : SpellUsable(abilities, debugAnnotations, haste, statTracker)
{
    public static bool HasNoCooldownDuringAvatar(int spellId) =>
        spellId == Spells.RadiantBlast.FSLID || spellId == Spells.CircleOfLight.FSLID;

    public override void BeginCooldown(int spellId, int? timestamp = null)
    {
        if (HasNoCooldownDuringAvatar(spellId) && Owner.SelectedCombatant.HasBuff(Spells.AvatarOfLightBuff, timestamp)) return;

        base.BeginCooldown(spellId, timestamp);
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AvatarOfLightBuff))]
    private void OnAvatarApplied(ApplyBuffEvent e)
    {
        EndCooldown(Spells.RadiantBlast.FSLID, e.Timestamp, restoreAllCharges: true);
        EndCooldown(Spells.CircleOfLight.FSLID, e.Timestamp, restoreAllCharges: true);
    }
}
