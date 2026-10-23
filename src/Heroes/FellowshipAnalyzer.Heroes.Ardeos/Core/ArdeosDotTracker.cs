using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;

namespace FellowshipAnalyzer.Heroes.Ardeos.Core;

public sealed class ArdeosDotTracker : DotTracker
{
    public override List<Dot> Dots => field ??= Equipped();

    /// <summary>True when the Explosivo legendary is equipped. Apocalypse then burns its targets.</summary>
    public bool ExplosivoEquipped => Owner.SelectedCombatant.HasLegendary(Legendaries.Explosivo.Id);

    private List<Dot> Equipped() =>
        ExplosivoEquipped
            ? ArdeosDots.All
            : [.. ArdeosDots.All.Where(dot => dot != ArdeosDots.Apocalypse)];
}
