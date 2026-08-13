using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Items;

namespace FellowshipAnalyzer.Heroes.Ardeos.Core;

public sealed class ArdeosDotTracker : DotTracker
{
    public override IReadOnlyList<Dot> Dots => field ??= Equipped();

    public bool BoomtasticRingEquipped => Owner.SelectedCombatant.HasItem(Items.RingOfBoomtasticExplosions.Id);

    private IReadOnlyList<Dot> Equipped() =>
        BoomtasticRingEquipped
            ? ArdeosDots.All
            : [.. ArdeosDots.All.Where(dot => dot != ArdeosDots.Apocalypse)];
}
