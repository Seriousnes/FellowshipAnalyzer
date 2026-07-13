namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A pull-lifetime analysis module. A fresh instance is constructed for every pull its
/// <c>[ForPull]</c> filter matches, accumulates state from that pull's events, and is retained on
/// the pull read surfaces when the pull ends. Guide and statistics components read the analyzer's
/// public properties and methods directly; there is no intermediate result projection.
/// </summary>
public class Analyzer : EventSubscriber, IAnalyzerSurface
{
    public const int SELECTED_PLAYER = 1;
    public const int SELECTED_PLAYER_PET = 2;

    /// <summary>
    /// Called once when this analyzer's pull ends, before the instance is exposed on the pull
    /// read surfaces. Override to finalize accumulated state (close still-open windows, compute
    /// derived aggregates); everything public must be readable after this returns.
    /// </summary>
    public virtual void OnPullEnd() { }

    /// <summary>
    /// The type under which <paramref name="analyzerType"/> is exposed on pull read surfaces: its
    /// <see cref="IAnalyzerSurface"/> marker interface if it implements one, otherwise the topmost
    /// ancestor deriving directly from <see cref="Analyzer"/>. Shape-specialized analyzers that
    /// share a surface (disjoint <c>[ForPull]</c> filters) thereby feed one cross-pull stream.
    /// </summary>
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
        foreach (var i in analyzerType.GetInterfaces())
            if (i != typeof(IAnalyzerSurface) && typeof(IAnalyzerSurface).IsAssignableFrom(i))
                return i;
        return null;
    }
}
