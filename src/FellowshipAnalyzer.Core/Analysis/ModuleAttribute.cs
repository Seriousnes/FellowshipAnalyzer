namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Marker attribute for analysis modules. Lets source generators discover modules without
/// requiring base-class membership.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModuleAttribute : Attribute
{
}
