namespace FellowshipAnalyzer.Core.Common.Items;

/// <summary>Generic legendary items whose Chronoshift-granting behavior is shared across the heroes that can equip them. The generated partial (built from the <c>items</c> scope of <c>data/spelldb.json</c>) adds the spells those items grant.</summary>
public partial class Items : IItemRegistry
{
    /// <summary>The staff legendary that grants the Chronoshift ability (spell 1558) to Ardeos.</summary>
    public static Item AshasChronoshiftSpire { get; } = new(5326, "Asha's Chronoshift Spire", "Tex_staff_05_b.jpg");

    /// <summary>
    /// IDs of every weapon that grants the Chronoshift ability (spell 1558). The granting weapon is
    /// hero-specific: Asha's Chronoshift Spire for Rime (5333) and Ardeos (5326), and Asha's
    /// Chronoshift Bow for Elarion (5325).
    /// </summary>
    public static IReadOnlySet<int> ChronoshiftGrantingItemIds { get; } = new HashSet<int>
    {
        5333,
        AshasChronoshiftSpire.Id,
        5325,
    };
}
