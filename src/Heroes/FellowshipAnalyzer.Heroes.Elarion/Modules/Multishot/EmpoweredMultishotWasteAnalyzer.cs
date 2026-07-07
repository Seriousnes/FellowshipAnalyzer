using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Multishot casts and how many of them were "regular" while an Empowered Multishot charge
/// was available. Empowered Multishot should always be consumed before regular casts. Multishot is
/// the AoE filler, so this is scored on multi-target (trash) pulls. Empowered Multishot comes from
/// the Focused Expanse talent; <see cref="EmpoweredMultishotWasteReport.BuildActive"/> records
/// whether the analysed player runs that build.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed partial class EmpoweredMultishotWasteAnalyzer : Analyzer<EmpoweredMultishotWasteReport>
{
    private bool _empoweredAvailable;

    private int _regularCasts;
    private int _empoweredConsumed;
    private int _wastedExpirations;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EmpoweredMultishotBuff))]
    private void OnApplyBuff(ApplyBuffEvent e)
    {
        _empoweredAvailable = true;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EmpoweredMultishotBuff))]
    private void OnRemoveBuff(RemoveBuffEvent e)
    {
        if (_empoweredAvailable)
        {
            _wastedExpirations++;
        }

        _empoweredAvailable = false;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Multishot))]
    private void OnMultishot(CastEvent e)
    {
        if (_empoweredAvailable)
        {
            _empoweredConsumed++;
            _empoweredAvailable = false;
        }
        else
        {
            _regularCasts++;
        }
    }

    public override EmpoweredMultishotWasteReport OnPullEnd() =>
        new(
            _regularCasts,
            _empoweredConsumed,
            _wastedExpirations,
            Owner.SelectedCombatant.HasTalent(Talents.FocusedExpanse.Id));
}
