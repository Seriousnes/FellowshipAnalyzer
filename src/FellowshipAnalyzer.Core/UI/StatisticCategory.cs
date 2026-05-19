namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// Sections rendered on the Statistics tab, in declaration order.
/// Add a new section by inserting a value at the desired position — the renderer
/// in <c>Report.razor</c> picks it up automatically.
/// </summary>
public enum StatisticCategory
{
    General,
    Resources,
    Cooldowns,
    Talents,
    Items,
    Other,
}
