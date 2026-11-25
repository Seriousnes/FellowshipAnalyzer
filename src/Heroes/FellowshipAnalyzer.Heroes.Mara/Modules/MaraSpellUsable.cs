using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed partial class MaraSpellUsable(
    Lazy<CoreAbilities> abilities,
    Lazy<DebugAnnotations> debugAnnotations,
    Lazy<Haste> haste,
    Lazy<StatTracker> statTracker) : SpellUsable(abilities, debugAnnotations, haste, statTracker)
{
    public override void BeginCooldown(int spellId, int? timestamp = null)
    {
        if (spellId == Spells.BroodingShadows.FSLID && Owner.CurrentPull is null) return;

        base.BeginCooldown(spellId, timestamp);
    }
}
