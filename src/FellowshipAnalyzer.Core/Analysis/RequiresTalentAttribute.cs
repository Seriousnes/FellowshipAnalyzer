namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Gates module construction on the selected combatant having a specific talent. Applied to a
/// dungeon-lifetime module class, it drives the source-generated <c>IsModuleActive</c> override to
/// emit a <c>context.SelectedCombatant.HasTalent(<see cref="TalentId"/>)</c> check. Applied to a
/// pull-lifetime <see cref="Analyzer"/>, the same check joins its <see cref="ForPullAttribute"/>
/// gate in the generated <c>GetAnalyzerTypes(PullStartEvent)</c>, so a build without the talent constructs no
/// instance and the analyzer's cross-pull read path stays empty.
/// </summary>
/// <param name="talentId">
/// The native talent id (the value stored on the hand-written <c>Talents</c> entry, e.g.
/// <c>ArdeosTalents.RollingFlames</c>). <see cref="FullCombatant.HasTalent(int)"/> decodes the log's
/// FSL talent encoding internally, so pass the native id here — not the FSL-encoded value.
/// </param>
/// <remarks>
/// <para>
/// Repeatable: applying multiple <c>[RequiresTalent]</c> attributes requires <em>all</em> of the
/// named talents (the generated checks are combined with <c>&amp;&amp;</c>). Combining with
/// <see cref="ActiveWhenAttribute{TPredicate}"/> ANDs the predicate with the talent checks.
/// </para>
/// <para>
/// Like <see cref="ActiveWhenAttribute{TPredicate}"/>, this is evaluated against the parse context
/// built before dispatch: when the check is false the module or analyzer is never instantiated, its
/// constructor never runs, and its <c>[On&lt;T&gt;]</c> handlers never subscribe.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequiresTalentAttribute(int talentId) : Attribute
{
    /// <summary>The native talent id the selected combatant must have for the module to activate.</summary>
    public int TalentId { get; } = talentId;
}
