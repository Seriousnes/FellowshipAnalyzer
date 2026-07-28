using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;

using Items = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public sealed class GundeAuras : Auras
{
    public override IEnumerable<SpellbookAura> GetAuras()
    {
        return [
            new()
            {
                SpellId = Spells.ReignInBloodSelfBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.BloodboundSpiritSelfBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Spells.OwedInBloodSelfBuff.FSLID,
                TimelineHighlight = true,
            },
            new()
            {
                SpellId = Items.GloriousPurpose.FSLID,
                TimelineHighlight = true,
            },
        ];
    }
}
