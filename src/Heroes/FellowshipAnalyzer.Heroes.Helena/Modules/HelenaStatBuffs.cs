using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Registers the stat effects Helena's own kit grants that the shared tables do not cover. Martial Command
/// raises Spirit by 5% for as long as the talent is taken, which no event marks, so it is handed to
/// <see cref="StatTracker"/> as a passive bonus.
/// </summary>
[Dependency<StatTracker>]
public sealed partial class HelenaStatBuffs : Analyzer
{
    private const double MartialCommandSpirit = 0.05;

    [On<DungeonStartEvent>]
    private void OnDungeonStart(DungeonStartEvent e)
    {
        if (Owner.SelectedCombatant.HasTalent(Talents.MartialCommand.Id))
            StatTracker.SetPassiveBonus(Talents.MartialCommand, new PassiveStatBonus { Spirit = MartialCommandSpirit }, e);
    }
}
