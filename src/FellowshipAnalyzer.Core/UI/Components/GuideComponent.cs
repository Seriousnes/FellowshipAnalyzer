using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.UI.Guides;

using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Base class for Guide components
/// </summary>
public abstract class GuideComponent<TParser> : ReportComponent<TParser> where TParser : CombatLogParser
{
    /// <summary>
    /// The enclosing <see cref="Section"/>'s activity ledger, when the guide is nested in one.
    /// The guide reports its <see cref="IsActive"/> result to it on every parameter set.
    /// </summary>
    [CascadingParameter] public GuideSectionScope? SectionScope { get; set; }

    /// <summary>
    /// Returns <c>true</c> if the component is active, typically based on talent or gear of the <see cref="CombatLogParser.SelectedCombatant"/>    ///
    /// </summary>
    /// <remarks>Defaults to <c>true</c> if not overridden</remarks>
    protected virtual bool IsActive() => true;

    /// <summary>
    /// Suppresses rendering the component when <see cref="IsActive"/> is <c>false</c>
    /// </summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);

        var active = IsActive();
        SectionScope?.Report(this, active);

        return active ? base.SetParametersAsync(ParameterView.Empty) : Task.CompletedTask;
    }
}
