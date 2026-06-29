namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>Classifies the kind of gap detected during a merge pass.</summary>
public enum GapKind
{
    /// <summary>A spell was built with no name from any source.</summary>
    MissingName,
    /// <summary>A spell was built with no icon from any source.</summary>
    MissingIcon,
    /// <summary>An override tried to add a new member but supplied no <c>id</c>.</summary>
    MissingId,
    /// <summary>A named effect in spell_data was not linked to any hero ability.</summary>
    UnresolvedEffect,
}

/// <summary>A detected gap in the merged spell data.</summary>
public record Gap(string Scope, string Member, GapKind Kind);

/// <summary>The output of <see cref="MergeEngine.Run"/>: selected spells and detected gaps.</summary>
public record MergeResult(IReadOnlyList<MergedSpell> Spells, IReadOnlyList<Gap> Gaps);
