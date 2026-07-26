using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed class MaraAuras : Auras
{
    public override IEnumerable<SpellbookAura> GetAuras()
    {
        return [
            new()
            {
                SpellId = Spells.MaidenOfDeathBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.MatriarchMacabreSelfBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.AssassinsGuileBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.BroodingShadowsBuff.FSLID,
                TimelineHighlight = true,
            },
        ];
    }
}
