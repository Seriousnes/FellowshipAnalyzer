using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="EmpoweredMultishotWasteAnalyzer"/> state, produced by
/// <see cref="EmpoweredMultishotWasteAnalyzer.OnPullEnd"/> for each multi-target pull.
/// <see cref="BuildActive"/> is true when the player runs the Focused Expanse (Empowered Multishot)
/// build; guides use it to hide the section for other builds.
/// </summary>
public sealed record EmpoweredMultishotWasteReport(
    int RegularCasts,
    int EmpoweredConsumed,
    int WastedExpirations,
    bool BuildActive) : IResult
{
    public EmpoweredMultishotWasteReport() : this(0, 0, 0, false) { }
}
