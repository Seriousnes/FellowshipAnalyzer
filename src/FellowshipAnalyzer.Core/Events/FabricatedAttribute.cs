namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marks an event class as always synthesized by FSA rather than sourced directly from the FellowshipLogs API.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class FabricatedAttribute : Attribute;
