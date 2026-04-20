namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks granular loading progress during report fetching and analysis.
/// Registered as a scoped DI service — one instance per Blazor circuit.
/// Call <see cref="Reset"/> before each report load.
/// Subscribe to <see cref="OnChanged"/> to react to state updates (e.g. trigger a re-render).
/// </summary>
public sealed class ReportLoadingTracker
{
    public enum StepState { Waiting, Loading, Ok }

    private StepState _fetchEventsState;
    private StepState _deserializeState;
    private StepState _normalizeState;
    private StepState _analyzeState;
    private int _analyzedEventCount;
    private int _totalEventCount;

    /// <summary>Fired whenever any tracked state changes.</summary>
    public event Action? OnChanged;

    /// <summary>Fetching combat events from the API or local cache.</summary>
    public StepState FetchEventsState
    {
        get => _fetchEventsState;
        set => Set(ref _fetchEventsState, value);
    }

    /// <summary>Deserializing the raw event JSON into typed event objects.</summary>
    public StepState DeserializeState
    {
        get => _deserializeState;
        set => Set(ref _deserializeState, value);
    }

    /// <summary>Running event normalizers (reordering, linking, fabrication).</summary>
    public StepState NormalizeState
    {
        get => _normalizeState;
        set => Set(ref _normalizeState, value);
    }

    /// <summary>Dispatching events through all analyzer modules.</summary>
    public StepState AnalyzeState
    {
        get => _analyzeState;
        set => Set(ref _analyzeState, value);
    }

    /// <summary>Number of events dispatched so far. Updated periodically during dispatch.</summary>
    public int AnalyzedEventCount
    {
        get => _analyzedEventCount;
        set => Set(ref _analyzedEventCount, value);
    }

    /// <summary>Total number of events to dispatch in this fight.</summary>
    public int TotalEventCount
    {
        get => _totalEventCount;
        set => Set(ref _totalEventCount, value);
    }

    /// <summary>
    /// Overall loading progress, 0–1.
    /// Each of the 4 steps contributes 25%; the Analyze step uses event-count ratio while in progress.
    /// </summary>
    public double Progress
    {
        get
        {
            var p = 0.0;
            if (_fetchEventsState == StepState.Ok) p += 0.25;
            if (_deserializeState == StepState.Ok) p += 0.25;
            if (_normalizeState == StepState.Ok) p += 0.25;
            if (_analyzeState == StepState.Ok)
                p += 0.25;
            else if (_analyzeState == StepState.Loading && _totalEventCount > 0)
                p += 0.25 * ((double)_analyzedEventCount / _totalEventCount);
            return p;
        }
    }

    /// <summary>Resets all step states to <see cref="StepState.Waiting"/> and clears event counts.</summary>
    public void Reset()
    {
        _fetchEventsState = StepState.Waiting;
        _deserializeState = StepState.Waiting;
        _normalizeState = StepState.Waiting;
        _analyzeState = StepState.Waiting;
        _analyzedEventCount = 0;
        _totalEventCount = 0;
        OnChanged?.Invoke();
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged?.Invoke();
    }
}
