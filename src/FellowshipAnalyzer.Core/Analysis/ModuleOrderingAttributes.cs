namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declarative ordering for modules that share a <see cref="Module.Priority"/>. Applied to a
/// module class, <c>[Before&lt;TOther&gt;]</c> requests that this module's handlers fire before
/// <typeparamref name="TOther"/>'s handlers when both subscribe to the same event.
/// <para>
/// The constraint is pairwise and relative only. <c>[After&lt;SpellUsable&gt;]</c> means that by the
/// time this module sees an event, <see cref="SpellUsable"/> has already seen it, and nothing more.
/// Neither module's absolute position in the dispatch order is fixed, and a module declaring no
/// constraint may run anywhere among them.
/// </para>
/// <para>
/// The generator topologically sorts module types from the union of
/// <see cref="BeforeAttribute{TOther}"/> and <see cref="AfterAttribute{TOther}"/> constraints and
/// emits the result through <c>GetModuleTypes()</c>, which is the order the parser constructs and
/// subscribes them in. Constraints are free to contradict each other, so nothing rejects a cycle;
/// the sort emits the modules it cannot place in declaration order, leaving their constraints
/// unhonoured rather than failing the build.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class BeforeAttribute<TOther> : Attribute
    where TOther : Module
{
}

/// <summary>
/// Counterpart to <see cref="BeforeAttribute{TOther}"/>. Applied to a module class,
/// <c>[After&lt;TOther&gt;]</c> requests that this module's handlers fire after
/// <typeparamref name="TOther"/>'s handlers when both subscribe to the same event.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AfterAttribute<TOther> : Attribute
    where TOther : Module
{
}
