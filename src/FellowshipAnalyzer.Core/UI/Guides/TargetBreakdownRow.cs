namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// One target's row in a <see cref="TargetBreakdown"/>.
/// </summary>
/// <param name="Target">
/// The target's display name. The caller resolves it, because the component has no parser access;
/// <c>Parser.ActorNames.GetValueOrDefault(unitId)</c> is the route the guides use.
/// </param>
/// <param name="Values">
/// The row's values, in the same order as the breakdown's columns and the same length as them. Use a
/// dash for a value the row has no figure for, rather than a shorter list.
/// </param>
public sealed record TargetBreakdownRow(string Target, IReadOnlyList<string> Values);
