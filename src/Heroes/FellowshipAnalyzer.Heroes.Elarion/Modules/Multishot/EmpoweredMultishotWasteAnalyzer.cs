using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Multishot casts and how many of them were "regular" while an Empowered Multishot charge
/// was available. Empowered Multishot should always be consumed before regular casts. Multishot is
/// the AoE filler, so this is scored on multi-target (trash) pulls. Empowered Multishot comes from
/// the Focused Expanse talent; <see cref="BuildActive"/> records whether the analysed player runs
/// that build.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed partial class EmpoweredMultishotWasteAnalyzer : Analyzer
{
    private bool _empoweredAvailable;

    public int RegularCasts { get; private set; }
    public int EmpoweredConsumed { get; private set; }
    public int WastedExpirations { get; private set; }

    /// <summary>True when the player runs the Focused Expanse (Empowered Multishot) build; guides
    /// use it to hide the section for other builds.</summary>
    public bool BuildActive { get; private set; }

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
            WastedExpirations++;
        }

        _empoweredAvailable = false;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Multishot))]
    private void OnMultishot(CastEvent e)
    {
        if (_empoweredAvailable)
        {
            EmpoweredConsumed++;
            _empoweredAvailable = false;
        }
        else
        {
            RegularCasts++;
        }
    }

    public override void OnPullEnd()
    {
        BuildActive = Owner.SelectedCombatant.HasTalent(Talents.FocusedExpanse.Id);
    }
}
