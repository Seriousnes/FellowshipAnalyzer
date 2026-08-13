namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The base for every module that subscribes to combat log events, declared with
/// <c>[On&lt;TEvent&gt;]</c> handlers the source generator wires into <see cref="EventEmitter"/>.
/// <para>
/// <c>[ForPull]</c> decides the lifetime. An analyzer declaring it is constructed fresh for every pull
/// its filter matches, accumulates state from that pull's events, and is retained on the pull read
/// surfaces when the pull ends. An analyzer without it is constructed once for the whole report.
/// Guide and statistics components read the analyzer's public properties and methods directly; there
/// is no intermediate result projection.
/// </para>
/// </summary>
public class Analyzer : Module, IAnalyzerSurface
{
    /// <summary>Bit flag matching <see cref="Actor.Player"/>, for filters built from a raw <c>int</c> actor mask instead of the <see cref="Actor"/> enum.</summary>
    public const int SELECTED_PLAYER = 1;

    internal int NumExecutions { get; set; }

    /// <summary>
    /// The pull this analyzer instance was constructed for, meaningful on an analyzer declaring
    /// <c>[ForPull]</c>. Assigned by the parser in <see cref="CombatLogParser.BeginPull"/>, so get-style
    /// accessors can reference pull-boundary values (e.g. <see cref="Pull.EndTime"/>) to close an
    /// interval still open when the pull ends, without a pull-end finalization pass.
    /// </summary>
    public Pull Pull { get; internal set; } = null!;

    /// <summary>
    /// Wires up subscriptions declared via <see cref="OnAttribute{TEvent}"/>. The source generator
    /// emits an override of this method on partial subclasses; the base implementation is empty.
    /// </summary>
    protected virtual void RegisterAttributeSubscriptions() { }

    /// <summary>
    /// Called once per analysis run, after every module has been constructed and after
    /// <see cref="Module.Owner"/> has been assigned. Wires up declarative <c>[On&lt;&gt;]</c>
    /// handlers via <see cref="RegisterAttributeSubscriptions"/>.
    /// </summary>
    public void RegisterSubscriptions()
    {
        RegisterAttributeSubscriptions();
    }

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
