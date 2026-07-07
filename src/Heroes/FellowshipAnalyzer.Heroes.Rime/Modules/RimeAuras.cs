using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed class RimeAuras : Auras
{
    public override IEnumerable<SpellbookAura> GetAuras()
    {
        return [
            new()
            {
                SpellId = Spells.WintersEmbrace.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.FlightOfTheNavirBuff.FSLID,
                TimelineHighlight = true
            },
            new()
            {
                SpellId = Spells.WrathOfWinterBuff.FSLID,              
                TimelineHighlight = true
            },
        ];
    }
}
