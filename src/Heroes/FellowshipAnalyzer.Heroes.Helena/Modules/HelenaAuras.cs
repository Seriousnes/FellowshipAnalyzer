using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public sealed class HelenaAuras : Auras
{
    public override IEnumerable<SpellbookAura> GetAuras() =>
    [
        new()
        {
            SpellId = Spells.ShieldsUpBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.IronWallBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.SiegebreakerBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.ShieldSlamAbsorb.FSLID,
            TimelineHighlight = true,
        },
    ];
}
