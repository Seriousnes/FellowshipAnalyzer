using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Utility;

using SpellRegistry = FellowshipAnalyzer.Core.Common.Spells.SpellRegistry;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// Turns <see cref="CooldownReductionResult"/> into <see cref="Checklist"/> lines.
/// </summary>
public static class CooldownReductionChecklist
{
    /// <summary>
    /// One line for <paramref name="spellId"/>, passing when none of the reduction aimed at it was
    /// wasted. The note omits either figure where it is zero.
    /// </summary>
    /// <param name="spellId">The ability the reduction was aimed at.</param>
    /// <param name="reduction">The reduction the line reports.</param>
    public static CheckItem Item(int spellId, CooldownReductionResult reduction) => new()
    {
        Label = Label(spellId),
        Pass = reduction.Wasted == 0,
        Note = Note(reduction),
        Title = $"{TimeFormat.Seconds(reduction.Total, 1)} generated, {TimeFormat.Seconds(reduction.Effective, 1)} shortened a running cooldown",
    };

    /// <summary>
    /// One line per ability, in the order given, leaving out every ability that generated no reduction.
    /// </summary>
    /// <param name="reductions">Each ability the reduction was aimed at, and what it generated.</param>
    public static IEnumerable<CheckItem> Items(
        IEnumerable<(int SpellId, CooldownReductionResult Reduction)> reductions) =>
        reductions
            .Where(entry => entry.Reduction.Total > 0)
            .Select(entry => Item(entry.SpellId, entry.Reduction));

    private static CheckLabel Label(int spellId)
    {
        var spell = SpellRegistry.MaybeGet(spellId);
        if (spell is not null) return spell;

        return $"Spell {spellId}";
    }

    private static string? Note(CooldownReductionResult reduction) => reduction switch
    {
        { Effective: 0, Wasted: 0 } => null,
        { Effective: 0 } => $"{TimeFormat.Seconds(reduction.Wasted, 1)} wasted",
        { Wasted: 0 } => $"{TimeFormat.Seconds(reduction.Effective, 1)} effective",
        _ => $"{TimeFormat.Seconds(reduction.Effective, 1)} effective, {TimeFormat.Seconds(reduction.Wasted, 1)} wasted",
    };
}
