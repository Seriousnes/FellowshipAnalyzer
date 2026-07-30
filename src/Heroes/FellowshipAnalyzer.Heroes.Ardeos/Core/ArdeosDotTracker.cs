using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Heroes.Ardeos.Core;

public sealed class ArdeosDotTracker : DotTracker
{
    public override IReadOnlyList<Dot> Dots => ArdeosDots.All;
}
