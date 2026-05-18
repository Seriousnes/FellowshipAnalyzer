namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declarative intent marker for analysis modules. Carries no behavior today; reserved
/// for future generators that need to discover modules without requiring base-class
/// membership. Modules will eventually be plain types with constructor deps,
/// <c>[On&lt;T&gt;]</c> handlers, and a <c>ToReport()</c> projection.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModuleAttribute : Attribute
{
}
