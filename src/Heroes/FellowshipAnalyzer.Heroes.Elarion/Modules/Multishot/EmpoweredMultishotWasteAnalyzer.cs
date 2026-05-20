using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Elarion.Modules.Multishot;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Multishot casts and how many of them were "regular" while an Empowered Multishot
/// charge was available. Empowered Multishot should always be consumed before regular casts.
/// </summary>
[ActiveWhen<HasEmpoweredMultishot>]
public sealed partial class EmpoweredMultishotWasteAnalyzer : Analyzer
{
    private bool _empoweredAvailable;
    private int? _empoweredAvailableSinceTimestamp;

    private int _regularCasts;
    private int _empoweredConsumed;
    private int _wastedExpirations;

    public int RegularCasts => _regularCasts;
    public int EmpoweredConsumed => _empoweredConsumed;
    public int WastedExpirations => _wastedExpirations;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EmpoweredMultishotBuff))]
    private void OnApplyBuff(ApplyBuffEvent e)
    {
        _empoweredAvailable = true;
        _empoweredAvailableSinceTimestamp = e.Timestamp;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EmpoweredMultishotBuff))]
    private void OnRemoveBuff(RemoveBuffEvent e)
    {
        if (_empoweredAvailable)
        {
            _wastedExpirations++;
        }

        _empoweredAvailable = false;
        _empoweredAvailableSinceTimestamp = null;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Multishot))]
    private void OnMultishot(CastEvent e)
    {
        if (_empoweredAvailable)
        {
            _empoweredConsumed++;
            _empoweredAvailable = false;
            _empoweredAvailableSinceTimestamp = null;
        }
        else
        {
            _regularCasts++;
        }
    }
}
