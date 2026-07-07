namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declarative module ordering. Applied to a module class, <c>[Before&lt;TOther&gt;]</c>
/// requests that this module's handlers fire before <typeparamref name="TOther"/>'s
/// handlers when both subscribe to the same event. The generator topologically sorts
/// module types from the union of <see cref="BeforeAttribute{TOther}"/> and
/// <see cref="AfterAttribute{TOther}"/> constraints and emits the resulting order through
/// <c>GetModuleTypes()</c> — the priority integer is still assigned by position, but
/// position is now declarative. Cycles fall back to declaration order so the build still
/// finishes.
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
