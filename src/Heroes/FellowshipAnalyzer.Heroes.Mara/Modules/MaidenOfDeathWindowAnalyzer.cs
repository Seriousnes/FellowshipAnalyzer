using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed record MaidenWindowCast(int Timestamp, int AbilityId);

public sealed record MaidenOfDeathRecast(int Timestamp, int GapMs, int HeldMs);

public sealed record MaidenOfDeathWindow
{
    public required int OpenedAt { get; init; }
    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    public bool HadMaidenOfDeath { get; init; }

    public bool HadMatriarchMacabre { get; init; }

    public bool Overlapped { get; init; }

    public IReadOnlyList<MaidenWindowCast> Casts { get; init; } = [];

    public int ScoredFinisherCasts { get; init; }

    public int FinisherComboPointsSpent { get; init; }

    public int GeneratorCasts { get; init; }

    public int ResetCasts { get; init; }

    public bool ResetCast => ResetCasts > 0;

    public int? EnergyAtClose { get; init; }
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class MaidenOfDeathWindowAnalyzer : Analyzer
{
    public static int RechargeMs { get; } = (int)Math.Round(Spells.MaidenOfDeath.Cooldown.GetValueOrDefault() * 1000);

    private static readonly int[] ScoredFinishers =
        [Spells.QueenFang.Id, Spells.ArachnidAssault.Id];

    private static readonly int[] Generators =
        [Spells.Backstab.Id, Spells.WidowBite.Id, Spells.SkitteringBlades.Id];

    private static readonly int[] Resets =
        [Spells.FinalStratagem.Id, Spells.MacabreStratagem.Id];

    private readonly List<CastEvent> _casts = [];
    private readonly List<BuffSpan> _spans = [];
    private readonly List<MaidenOfDeathRecast> _recasts = [];

    private BuffSpan? _openSpan;
    private bool _maidenUp;
    private bool _matriarchUp;
    private int _previousMaidenCast = -1;

    public IReadOnlyList<MaidenOfDeathWindow> Windows => field ??= Build();

    public int WindowCount => Windows.Count;

    public int OverlappedWindows => Windows.Count(window => window.Overlapped);

    public int WindowsWithReset => Windows.Count(window => window.ResetCast);

    public int TotalWindowMs => Windows.Sum(window => window.DurationMs);

    public int ScoredFinishersInWindows => Windows.Sum(window => window.ScoredFinisherCasts);

    public int ComboPointsSpentInWindows => Windows.Sum(window => window.FinisherComboPointsSpent);

    public int ResetCastsInWindows => Windows.Sum(window => window.ResetCasts);

    public double AverageFinishersPerWindow =>
        Windows.Count == 0 ? 0d : (double)ScoredFinishersInWindows / Windows.Count;

    public int MaidenOfDeathCasts { get; private set; }

    public int MatriarchMacabreCasts { get; private set; }

    public int ResetCasts { get; private set; }

    public IReadOnlyList<MaidenOfDeathRecast> MaidenOfDeathRecasts => _recasts;

    public int TotalHeldMs => _recasts.Sum(recast => recast.HeldMs);

    public double AverageHeldMs => _recasts.Count == 0 ? 0d : (double)TotalHeldMs / _recasts.Count;

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        _casts.Add(castEvent);

        var abilityId = castEvent.Ability.Id;

        if (Array.IndexOf(Resets, abilityId) >= 0)
            ResetCasts++;

        if (abilityId == Spells.MatriarchMacabre.Id)
            MatriarchMacabreCasts++;

        if (abilityId != Spells.MaidenOfDeath.Id)
            return;

        MaidenOfDeathCasts++;

        if (_previousMaidenCast >= 0)
        {
            var gap = castEvent.Timestamp - _previousMaidenCast;
            _recasts.Add(new MaidenOfDeathRecast(castEvent.Timestamp, gap, Held(gap)));
        }

        _previousMaidenCast = castEvent.Timestamp;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spells = [nameof(Spells.MaidenOfDeathBuff), nameof(Spells.MatriarchMacabreSelfBuff)])]
    private void OnBuffApplied(ApplyBuffEvent buffEvent)
    {
        var maiden = buffEvent.Ability.Id == Spells.MaidenOfDeathBuff.FSLID;
        if (maiden)
            _maidenUp = true;
        else
            _matriarchUp = true;

        var span = _openSpan ??= new BuffSpan(buffEvent.Timestamp);

        if (maiden)
            span.HadMaidenOfDeath = true;
        else
            span.HadMatriarchMacabre = true;

        if (_maidenUp && _matriarchUp)
            span.Overlapped = true;

        span.ClosedAt = Math.Max(span.ClosedAt, buffEvent.Timestamp);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spells = [nameof(Spells.MaidenOfDeathBuff), nameof(Spells.MatriarchMacabreSelfBuff)])]
    private void OnBuffRemoved(RemoveBuffEvent buffEvent)
    {
        if (buffEvent.Ability.Id == Spells.MaidenOfDeathBuff.FSLID)
            _maidenUp = false;
        else
            _matriarchUp = false;

        if (_openSpan is null)
            return;

        _openSpan.ClosedAt = Math.Max(_openSpan.ClosedAt, buffEvent.Timestamp);

        if (_maidenUp || _matriarchUp)
            return;

        _spans.Add(_openSpan);
        _openSpan = null;
    }

    private static int Held(int gapMs) => RechargeMs <= 0 ? 0 : Math.Max(0, gapMs - RechargeMs);

    private List<MaidenOfDeathWindow> Build()
    {
        var windows = new List<MaidenOfDeathWindow>(_spans.Count + 1);
        foreach (var span in _spans)
            windows.Add(BuildWindow(span, span.ClosedAt));

        if (_openSpan is not null)
            windows.Add(BuildWindow(_openSpan, Math.Max(_openSpan.ClosedAt, Pull.EndTime)));

        return windows;
    }

    private MaidenOfDeathWindow BuildWindow(BuffSpan span, int closedAt)
    {
        var casts = new List<MaidenWindowCast>();
        var finishers = 0;
        var comboPoints = 0;
        var generators = 0;
        var resets = 0;
        int? energy = null;

        foreach (var cast in _casts)
        {
            if (cast.Timestamp < span.OpenedAt || cast.Timestamp > closedAt)
                continue;

            var abilityId = cast.Ability.Id;
            casts.Add(new MaidenWindowCast(cast.Timestamp, abilityId));

            var resources = cast.SourceResources?.Resources;
            if (FindResource(resources, ResourceTypes.Primary) is { } primary)
                energy = primary.Amount;

            if (Array.IndexOf(ScoredFinishers, abilityId) >= 0)
            {
                finishers++;
                if (FindResource(resources, ResourceTypes.Secondary) is { } secondary)
                    comboPoints += secondary.Amount;
            }
            else if (Array.IndexOf(Generators, abilityId) >= 0)
            {
                generators++;
            }

            if (Array.IndexOf(Resets, abilityId) >= 0)
                resets++;
        }

        return new MaidenOfDeathWindow
        {
            OpenedAt = span.OpenedAt,
            ClosedAt = closedAt,
            HadMaidenOfDeath = span.HadMaidenOfDeath,
            HadMatriarchMacabre = span.HadMatriarchMacabre,
            Overlapped = span.Overlapped,
            Casts = casts,
            ScoredFinisherCasts = finishers,
            FinisherComboPointsSpent = comboPoints,
            GeneratorCasts = generators,
            ResetCasts = resets,
            EnergyAtClose = energy,
        };
    }

    private static ClassResource? FindResource(List<ClassResource>? resources, ResourceTypes type)
    {
        if (resources is null)
            return null;

        foreach (var resource in resources)
        {
            if (resource.Type == type)
                return resource;
        }

        return null;
    }

    private sealed class BuffSpan(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int ClosedAt { get; set; } = openedAt;
        public bool HadMaidenOfDeath { get; set; }
        public bool HadMatriarchMacabre { get; set; }
        public bool Overlapped { get; set; }
    }
}
