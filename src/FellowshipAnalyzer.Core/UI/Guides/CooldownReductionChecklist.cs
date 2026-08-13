using FellowshipAnalyzer.Core.Analysis;

using SpellRegistry = FellowshipAnalyzer.Core.Common.Spells.SpellRegistry;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// Turns <see cref="CooldownReductionResult"/> readings into <see cref="Checklist"/> lines, so a card
/// names each ability the reduction was aimed at beside how much of it shortened a running cooldown.
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
        Title = $"{Seconds(reduction.Total)} generated, {Seconds(reduction.Effective)} shortened a running cooldown",
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
        { Effective: 0 } => $"{Seconds(reduction.Wasted)} wasted",
        { Wasted: 0 } => $"{Seconds(reduction.Effective)} effective",
        _ => $"{Seconds(reduction.Effective)} effective, {Seconds(reduction.Wasted)} wasted",
    };

    private static string Seconds(int milliseconds) => $"{milliseconds / 1000d:0.#}s";
}
