using FellowshipAnalyzer.Core.Analysis;

using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Base class for Guide components
/// </summary>
public abstract class GuideComponent<TParser> : ReportComponent<TParser> where TParser : CombatLogParser
{
    /// <summary>
    /// Returns <c>true</c> if the component is active, typically based on talent or gear of the <see cref="CombatLogParser.SelectedCombatant"/>
    /// </summary>
    protected abstract bool IsActive();

    /// <summary>
    /// Suppresses rendering the component when <see cref="IsActive"/> is <c>false</c>
    /// </summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        return IsActive() ? base.SetParametersAsync(ParameterView.Empty) : Task.CompletedTask;
    }
}
