using System;
using System.Collections.Generic;
using System.Text;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

internal class FocusTracker : ResourceTracker
{
    protected override int? GetResourceCost(CastEvent e, ResourceTypes type)
    {
        var spell = SpellRegistry.MaybeGet(e.Ability.Guid);
        return type switch
        {
            ResourceTypes.Primary => spell?.FocusCost,
            _ => base.GetResourceCost(e, type),
        };
    }
}
