using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed class RimeAuras : Auras
{
    public override IEnumerable<SpellbookAura> GetAuras()
    {
        yield return new SpellbookAura
        {
            SpellId = Spells.WintersEmbrace.Guid,
            TimelineHighlight = true,
        };
    }
}
