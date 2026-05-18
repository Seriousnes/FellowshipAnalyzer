namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Replaces the mutable <see cref="Module.Active"/> flag with a compile-time activation
/// predicate evaluated at parser construction.
/// </summary>
/// <typeparam name="TPredicate">
/// A type implementing <see cref="IModuleActivePredicate"/>. Its static
/// <see cref="IModuleActivePredicate.IsActive(ParseContext)"/> method is called once per
/// analysis run before the module would otherwise be instantiated.
/// </typeparam>
/// <remarks>
/// <para>
/// Modules without this attribute keep today's behavior — the mutable <c>Active</c> flag
/// is consulted per event in the dispatch loop, so dynamic deactivation is still possible.
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
