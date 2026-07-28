using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>The auras whose windows are worth shading on Sylvie's timeline.</summary>
public sealed class SylvieAuras : Auras
{
    /// <inheritdoc/>
    public override IEnumerable<SpellbookAura> GetAuras() =>
    [
        new()
        {
            SpellId = Spells.FluttercallHealHot.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.FluttercallProtectBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.FluttercallEmbraceBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.SafeHavenIncreasedHealingTakenBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.IronleafWardBuff.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.NaturalProtectorAbsorb.FSLID,
            TimelineHighlight = true,
        },
        new()
        {
            SpellId = Spells.FluttercallJubileeBuff.FSLID,
            TimelineHighlight = true,
        },
    ];
}
