namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A pull-lifetime analysis module. A fresh instance is constructed for every pull its
/// <c>[ForPull]</c> filter matches, accumulates state from that pull's events, and is retained on
/// the pull read surfaces when the pull ends. Guide and statistics components read the analyzer's
/// public properties and methods directly; there is no intermediate result projection.
/// </summary>
public class Analyzer : EventSubscriber, IAnalyzerSurface
{
    /// <summary>Bit flag matching <see cref="Actor.Player"/>, for filters built from a raw <c>int</c> actor mask instead of the <see cref="Actor"/> enum.</summary>
    public const int SELECTED_PLAYER = 1;
    /// <summary>Bit flag matching <see cref="Actor.Pet"/>, for filters built from a raw <c>int</c> actor mask instead of the <see cref="Actor"/> enum.</summary>
    public const int SELECTED_PLAYER_PET = 2;

    /// <summary>
    /// The pull this analyzer instance was constructed for. Assigned by the parser in
    /// <see cref="CombatLogParser.BeginPull"/>, so get-style accessors can reference pull-boundary
    /// values (e.g. <see cref="Pull.EndTime"/>) to close an interval still open when the pull ends,
    /// without a pull-end finalization pass.
    /// </summary>
    public Pull Pull { get; internal set; } = null!;

    internal static Type GetSurfaceType(Type analyzerType)
    {
        if (FindSurfaceInterface(analyzerType) is { } surface) return surface;

        var type = analyzerType;
        while (type.BaseType is { } baseType && baseType != typeof(Analyzer))
            type = baseType;
        return type;
    }

    private static Type? FindSurfaceInterface(Type analyzerType)
    {
        Type? found = null;
        foreach (var i in analyzerType.GetInterfaces())
        {
            if (i == typeof(IAnalyzerSurface) || !typeof(IAnalyzerSurface).IsAssignableFrom(i)) continue;
            if (found is not null)
                throw new InvalidOperationException(
                    $"Analyzer '{analyzerType.Name}' implements multiple surface interfaces ('{found.Name}' and '{i.Name}'); an analyzer's surface must be unambiguous (see diagnostic FA0017).");
            found = i;
        }
        return found;
    }
}
