namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// A semantic class and the token that colours it. A component applies the class rather than
/// declaring a colour custom property of its own.
/// </summary>
/// <param name="ClassName">The flat hyphenated class name, for example <c>perfect</c>.</param>
/// <param name="TokenName">The token name without its <c>--fa-</c> prefix, for example <c>perf-perfect</c>.</param>
public readonly record struct FaSemanticClass(string ClassName, string TokenName);

/// <summary>
/// The semantic classes the stylesheet mints: the four performance tiers, the three hero roles and
/// the twelve event types. Derived from the token names, so the class set cannot fall out of step
/// with the token set.
///
/// <c>RoleUnknown</c> has no class. It is a low-alpha structural grey standing in for "no role",
/// so it reads as an accent edge but would render text near-invisible.
/// </summary>
public static class FaSemantic
{
    /// <summary>Every semantic class, in the order the generated map declares them.</summary>
    public static IReadOnlyList<FaSemanticClass> All { get; } =
    [
        Tier("perfect", nameof(FaPalette.PerfPerfect)),
        Tier("good", nameof(FaPalette.PerfGood)),
        Tier("ok", nameof(FaPalette.PerfOk)),
        Tier("fail", nameof(FaPalette.PerfFail)),

        Tier("tank", nameof(FaPalette.RoleTank)),
        Tier("healer", nameof(FaPalette.RoleHealer)),
        Tier("dps", nameof(FaPalette.RoleDps)),

        Tier("cast", nameof(FaPalette.EvCast)),
        Tier("damage", nameof(FaPalette.EvDamage)),
        Tier("heal", nameof(FaPalette.EvHeal)),
        Tier("buff", nameof(FaPalette.EvBuff)),
        Tier("buff-fade", nameof(FaPalette.EvBuffFade)),
        Tier("debuff", nameof(FaPalette.EvDebuff)),
        Tier("death", nameof(FaPalette.EvDeath)),
        Tier("resource", nameof(FaPalette.EvResource)),
        Tier("system", nameof(FaPalette.EvSystem)),
        Tier("modified", nameof(FaPalette.EvModified)),
        Tier("fabricated", nameof(FaPalette.EvFabricated)),
        Tier("reordered", nameof(FaPalette.EvReordered)),
    ];

    private static FaSemanticClass Tier(string className, string property) =>
        new(className, FaToken.KebabCase(property));
}
