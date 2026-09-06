using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// The mana Chrona Tap's expiries returned, and the part lost at the cap.
/// </summary>
/// <remarks>
/// <para>
/// Registered dungeon-lifetime, so both figures span the whole report, including between pulls.
/// </para>
/// <para>
/// <c>[Before&lt;ChronaTracker&gt;]</c> puts this analyzer ahead of the tracker, so the mana amount
/// read at an expiry is the amount held before the return.
/// </para>
/// </remarks>
[RequiresTalent(AeonaTalents.ChronaTap)]
[Dependency<ChronaTracker>]
[Before<ChronaTracker>]
public sealed partial class ChronaTapAnalyzer : Analyzer
{
    /// <summary>The stacks Chrona Tap holds at most.</summary>
    public const int MaximumStacks = 10;

    private int _stacks;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    /// <summary>The mana Chrona Tap's expiries returned.</summary>
    public int ManaReturned { get; private set; }

    /// <summary>The part of <see cref="ManaReturned"/> lost at the cap.</summary>
    public int ManaLostAtCap { get; private set; }

    /// <summary>The mana one stack returns at expiry.</summary>
    public int ManaPerStack => (int)Math.Round(PerStackShare * ChronaTracker.MaxOf(ResourceTypes.Mana));

    private static double PerStackShare => Talents.ChronaTap.ResourceGeneration?.Amount ?? 0;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnApplied(ApplyBuffEvent e) => _stacks = 1;

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnStackApplied(ApplyBuffStackEvent e) => _stacks = Math.Clamp(e.Stack, 0, MaximumStacks);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnStackRemoved(RemoveBuffStackEvent e) => Expire(e.Timestamp, e.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnRemoved(RemoveBuffEvent e) => Expire(e.Timestamp, 0);

    private void Expire(int timestamp, int remaining)
    {
        var capped = Math.Clamp(remaining, 0, MaximumStacks);
        if (capped >= _stacks)
        {
            _stacks = capped;
            return;
        }

        var returned = (_stacks - capped) * ManaPerStack;
        var room = Math.Max(0, ChronaTracker.MaxOf(ResourceTypes.Mana) - ChronaTracker.AmountAt(ResourceTypes.Mana, timestamp));

        ManaReturned += returned;
        ManaLostAtCap += Math.Max(0, returned - room);
        _stacks = capped;
    }
}
