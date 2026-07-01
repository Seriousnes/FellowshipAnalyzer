using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

public class FocusTracker : ResourceTracker
{
    public FocusTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        DisplayNameOverrides[ResourceTypes.Primary] = "Focus";
    }

    protected override int? GetResourceCost(CastEvent e, ResourceTypes type)
    {
        var spell = SpellRegistry.MaybeGet(e.Ability.FSLID);
        return type switch
        {
            ResourceTypes.Primary => spell?.FocusCost,
            _ => base.GetResourceCost(e, type),
        };
    }
}
