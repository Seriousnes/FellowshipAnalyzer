namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// One open window of an aura on a unit, in milliseconds. Multi-instance auras open a window per
/// application, so concurrent windows of one effect on one unit are expected.
/// </summary>
public readonly record struct AuraWindow(int Start, int End)
{
    /// <summary>How long the window last in milliseconds.</summary>
    public int Duration => End - Start;
}
