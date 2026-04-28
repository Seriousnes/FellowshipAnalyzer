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
                SpellId = Spells.EventHorizonBuff.Guid,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.SkystridersGraceBuff.Guid,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.CelestialImpetus.Guid,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.ImpendingHeartseeker.Guid,
                TimelineHighlight = true,
            },
        ];
    }
}
