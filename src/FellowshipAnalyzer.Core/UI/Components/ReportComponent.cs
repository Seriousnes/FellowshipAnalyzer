using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Base class for any report-scoped component: the parser that produced the analysis being
/// rendered, plus report-wide context such as dungeon time bounds. Cascading values are supplied by
/// the report shell (<c>Report.razor</c>) and inherited automatically by derived components via
/// <c>@inherits ReportComponent&lt;{Hero}CombatLogParser&gt;</c>.
/// </summary>
/// <remarks>
/// Future report-scoped cascades (e.g. report filter context, selected player) should be
/// added here as additional <c>[CascadingParameter]</c> properties so all consumers pick
/// them up uniformly.
/// </remarks>
/// <typeparam name="TParser">
/// The hero's <see cref="CombatLogParser"/> subclass, which gives <see cref="Parser"/> its type.
/// Inherit the non-generic <see cref="ReportComponent"/> for a component that works across heroes.
/// </typeparam>
public abstract class ReportComponent<TParser> : ComponentBase where TParser : CombatLogParser
{
    /// <summary>The report's dungeon start and end timestamps, cascaded from the report shell.</summary>
    [CascadingParameter]
    protected DungeonTimeContext DungeonTime { get; set; } = null!;

    /// <summary>The hero analysis result being rendered, cascaded from the report shell.</summary>
    [CascadingParameter]
    protected HeroAnalysisResult Result {get; set; } = null!;

    /// <summary>The parser that produced <see cref="Result"/>, cascaded from the report shell as <see cref="Parser"/>'s untyped source.</summary>
    [CascadingParameter]
    public IHeroAnalyzer Owner { get; set; } = null!;

    /// <summary>
    /// The pull the report is currently clamped to, or <c>null</c> for the whole dungeon ("Entire
    /// Dungeon"). Supplied by the report shell as a named cascade so every report-scoped component
    /// crops its view to the same pull window.
    /// </summary>
    [CascadingParameter(Name = "SelectedPull")]
    protected PullStartEvent? SelectedPull { get; set; }

    /// <summary>
    /// The parser that produced the analysis being rendered, strongly typed. Reached through the
    /// report shell's cascade, never by injection: parsers are transient, one per analysis, so an
    /// injected parser has analyzed nothing. A component rendered outside the report shell has no
    /// cascade to read and fails here.
    /// </summary>
    protected TParser Parser => (TParser)Owner;

    /// <summary>Absolute dungeon start timestamp in milliseconds.</summary>
    protected int DungeonStartTime => DungeonTime.StartTime;

    /// <summary>Absolute dungeon end timestamp in milliseconds.</summary>
    protected int DungeonEndTime => DungeonTime.EndTime;
}

/// <summary>
/// A report-scoped component that works across every hero, reading
/// <see cref="ReportComponent{TParser}.Parser"/> as the base <see cref="CombatLogParser"/>.
/// </summary>
public abstract class ReportComponent : ReportComponent<CombatLogParser>;
