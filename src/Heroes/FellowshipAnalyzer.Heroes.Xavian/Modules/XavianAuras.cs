using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Xavian;

namespace FellowshipAnalyzer.Heroes.Xavian.Modules;

public sealed class XavianAuras : Auras
{
    public override IEnumerable<SpellbookAura> GetAuras() =>
    [
        new()
        {
            SpellId = Spells.ShiningHaloSelfBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.DecreeOfTheSunSelfBuffImmunity.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.DecreeOfTheSunSelfBuffDamageReduction.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.OmegaReprievalBuff.FSLID,
            TimelineHighlight = true,
        },
    ];
}
