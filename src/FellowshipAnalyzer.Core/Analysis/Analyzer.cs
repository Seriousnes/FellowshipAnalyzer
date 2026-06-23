namespace FellowshipAnalyzer.Core.Analysis;

public class Analyzer : EventSubscriber
{
    public const int SELECTED_PLAYER = 1;
    public const int SELECTED_PLAYER_PET = 2;

    /// <summary>
    /// Produces this analyzer's per-pull result when its pull ends, or <c>null</c> for a
    /// side-effect-only analyzer. Overridden by <see cref="Analyzer{TResult}"/>.
    /// </summary>
    internal virtual IResult? CaptureResult() => null;

    /// <summary>
    /// The declared result type (<c>TResult</c> of <see cref="Analyzer{TResult}"/>), or <c>null</c>
    /// for a side-effect-only analyzer. Used to key the per-pull result store by the declared type
    /// rather than the runtime type, so a non-sealed result record stays readable via the typed
    /// read paths.
    /// </summary>
    internal virtual Type? ResultType => null;
}

/// <summary>
/// An <see cref="Analyzer"/> that produces a typed <typeparamref name="TResult"/> for each pull it
/// runs on. A fresh instance is constructed per pull, so <see cref="OnPullEnd"/> sees only that
/// pull's accumulated state.
/// </summary>
public abstract class Analyzer<TResult> : Analyzer where TResult : IResult, new()
{
    public virtual TResult OnPullEnd() => new();

    internal override IResult CaptureResult() => OnPullEnd();

    internal override Type ResultType => typeof(TResult);
}
