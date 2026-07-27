using System.Diagnostics;
using System.Runtime.CompilerServices;

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
    private StepState _prepareDisplayState;
    private int _deserializedEventCount;
    private int _totalDeserializeEventCount;
    private int _analyzedEventCount;
    private int _totalEventCount;
    private int _normalizedCount;
    private int _totalNormalizerCount;

    private readonly Dictionary<string, Stopwatch> _stepStopwatches = new(StringComparer.Ordinal);

    /// <summary>Fired whenever any tracked state changes.</summary>
    public event Action? OnChanged;

    /// <summary>Fetching combat events from the API or local cache.</summary>
    public StepState FetchEventsState
    {
        get => _fetchEventsState;
        set => SetStepState(ref _fetchEventsState, value);
    }

    /// <summary>Deserializing the raw event JSON into typed event objects.</summary>
    public StepState DeserializeState
    {
        get => _deserializeState;
        set => SetStepState(ref _deserializeState, value);
    }

    /// <summary>Number of raw events deserialized so far.</summary>
    public int DeserializedEventCount
    {
        get => _deserializedEventCount;
        set => Set(ref _deserializedEventCount, value);
    }

    /// <summary>Total number of raw events to deserialize.</summary>
    public int TotalDeserializeEventCount
    {
        get => _totalDeserializeEventCount;
        set => Set(ref _totalDeserializeEventCount, value);
    }

    /// <summary>Running event normalizers (reordering, linking, fabrication).</summary>
    public StepState NormalizeState
    {
        get => _normalizeState;
        set => SetStepState(ref _normalizeState, value);
    }

    /// <summary>Dispatching events through all analyzer modules.</summary>
    public StepState AnalyzeState
    {
        get => _analyzeState;
        set => SetStepState(ref _analyzeState, value);
    }

    /// <summary>Preparing the display / rendering results.</summary>
    public StepState PrepareDisplayState
    {
        get => _prepareDisplayState;
        set => SetStepState(ref _prepareDisplayState, value);
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

    /// <summary>Number of normalizers that have completed so far.</summary>
    public int NormalizedCount
    {
        get => _normalizedCount;
        set => Set(ref _normalizedCount, value);
    }

    /// <summary>Total number of normalizers to run.</summary>
    public int TotalNormalizerCount
    {
        get => _totalNormalizerCount;
        set => Set(ref _totalNormalizerCount, value);
    }

    /// <summary>
    /// Returns the sub-progress (0–1) for a step that is currently in <see cref="StepState.Loading"/>.
    /// Returns 0 for Waiting, 1 for Ok.
    /// </summary>
    public double DeserializeSubProgress => _deserializeState switch
    {
        StepState.Ok => 1,
        StepState.Loading when _totalDeserializeEventCount > 0 => (double)_deserializedEventCount / _totalDeserializeEventCount,
        _ => 0,
    };

    /// <summary>
    /// Returns the sub-progress (0–1) for normalizer execution.
    /// </summary>
    public double NormalizeSubProgress => _normalizeState switch
    {
        StepState.Ok => 1,
        StepState.Loading when _totalNormalizerCount > 0 => (double)_normalizedCount / _totalNormalizerCount,
        _ => 0,
    };

    /// <summary>
    /// Returns the sub-progress (0–1) for event analysis.
    /// </summary>
    public double AnalyzeSubProgress => _analyzeState switch
    {
        StepState.Ok => 1,
        StepState.Loading when _totalEventCount > 0 => (double)_analyzedEventCount / _totalEventCount,
        _ => 0,
    };

    /// <summary>
    /// Overall loading progress, 0–1.
    /// Each of the 5 steps contributes 20%; Normalize and Analyze use sub-progress while in progress.
    /// </summary>
    public double Progress
    {
        get
        {
            const double stepWeight = 0.2;
            var p = 0.0;
            if (_fetchEventsState == StepState.Ok) p += stepWeight;
            if (_deserializeState == StepState.Ok)
                p += stepWeight;
            else if (_deserializeState == StepState.Loading && _totalDeserializeEventCount > 0)
                p += stepWeight * ((double)_deserializedEventCount / _totalDeserializeEventCount);
            if (_normalizeState == StepState.Ok)
                p += stepWeight;
            else if (_normalizeState == StepState.Loading && _totalNormalizerCount > 0)
                p += stepWeight * ((double)_normalizedCount / _totalNormalizerCount);
            if (_analyzeState == StepState.Ok)
                p += stepWeight;
            else if (_analyzeState == StepState.Loading && _totalEventCount > 0)
                p += stepWeight * ((double)_analyzedEventCount / _totalEventCount);
            if (_prepareDisplayState == StepState.Ok) p += stepWeight;
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
        _prepareDisplayState = StepState.Waiting;
        _deserializedEventCount = 0;
        _totalDeserializeEventCount = 0;
        _analyzedEventCount = 0;
        _totalEventCount = 0;
        _normalizedCount = 0;
        _totalNormalizerCount = 0;
        _stepStopwatches.Clear();
        OnChanged?.Invoke();
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Step setter that also records timing. Logs elapsed milliseconds to the console
    /// when a step transitions from <see cref="StepState.Loading"/> to <see cref="StepState.Ok"/>.
    /// </summary>
    private void SetStepState(ref StepState field, StepState value, [CallerMemberName] string stepName = "")
    {
        if (field == value) return;
        var previous = field;
        field = value;

        if (value is StepState.Loading)
        {
            if (!_stepStopwatches.TryGetValue(stepName, out var sw))
            {
                sw = new Stopwatch();
                _stepStopwatches[stepName] = sw;
            }
            sw.Restart();
        }
        else if (value is StepState.Ok && previous is StepState.Loading && _stepStopwatches.TryGetValue(stepName, out var sw))
        {
            sw.Stop();
            Console.WriteLine($"[ReportLoadingTracker] {stepName}: {sw.ElapsedMilliseconds} ms");
        }

        OnChanged?.Invoke();
    }
}
