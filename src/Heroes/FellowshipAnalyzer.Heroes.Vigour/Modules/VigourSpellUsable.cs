using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public sealed partial class VigourSpellUsable(
    Lazy<CoreAbilities> abilities,
    Lazy<DebugAnnotations> debugAnnotations,
    Lazy<Haste> haste,
    Lazy<StatTracker> statTracker) : SpellUsable(abilities, debugAnnotations, haste, statTracker)
{
    public override void BeginCooldown(int spellId, int? timestamp = null)
    {
        if (AvatarOfLightAnalyzer.HasNoCooldown(spellId) && Owner.SelectedCombatant.HasBuff(Spells.AvatarOfLightBuff, timestamp)) return;

        base.BeginCooldown(spellId, timestamp);
    }
}
