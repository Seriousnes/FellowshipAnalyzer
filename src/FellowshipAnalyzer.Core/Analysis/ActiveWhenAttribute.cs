namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Gates module construction behind a compile-time activation predicate evaluated at parser
/// construction. Use this when activation is known up front; for dynamic deactivation during
/// dispatch, use the mutable <see cref="Module.Active"/> flag instead.
/// </summary>
/// <typeparam name="TPredicate">
/// A type implementing <see cref="IModuleActivePredicate"/>. Its static
/// <see cref="IModuleActivePredicate.IsActive(ParseContext)"/> method is called once per
/// analysis run before the module would otherwise be instantiated.
/// </typeparam>
/// <remarks>
/// <para>
/// Modules without this attribute fall back to the mutable <c>Active</c> flag, which is
/// consulted per event in the dispatch loop and allows dynamic deactivation.
/// </para>
/// <para>
/// When the predicate returns <c>false</c>, the source generator's parser switch skips
/// resolution entirely; no constructor runs, no <c>[On&lt;T&gt;]</c> handlers subscribe,
/// and the generated <c>BuildTypedReport()</c> contributes <c>null</c> for that module.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ActiveWhenAttribute<TPredicate> : Attribute
    where TPredicate : IModuleActivePredicate
{
}
