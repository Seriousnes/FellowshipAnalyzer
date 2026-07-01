using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

public sealed class ElarionAuras : Auras
{
    public override IEnumerable<SpellbookAura> GetAuras()
    {
        return [
            new()
            {
                SpellId = Spells.EventHorizonBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.SkystridersGraceBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.CelestialImpetus.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.ImpendingHeartseeker.FSLID,
                TimelineHighlight = true,
            },
        ];
    }
}
