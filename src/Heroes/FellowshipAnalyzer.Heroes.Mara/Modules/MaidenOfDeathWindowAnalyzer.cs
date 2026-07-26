using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>One player cast recorded inside a burst window, carrying the ability it pressed.</summary>
public sealed record MaidenWindowCast(int Timestamp, int AbilityId);

/// <summary>
/// One Maiden of Death recast measured against the previous one:
/// <paramref name="GapMs"/> is the interval between the two casts and <paramref name="HeldMs"/> is the
/// part of that interval beyond the ability's static recharge, when the charge was ready and unspent.
/// </summary>
public sealed record MaidenOfDeathRecast(int Timestamp, int GapMs, int HeldMs);

/// <summary>
/// One contiguous burst window: the stretch from the first Maiden of Death or Matriarch Macabre
/// self-buff application to the removal that leaves neither standing, together with everything the
/// player pressed inside it. Buff chains that overlap are one window, not two.
/// </summary>
public sealed record MaidenOfDeathWindow
{
    public required int OpenedAt { get; init; }
    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    /// <summary>Whether the Maiden of Death self-buff took part in this window.</summary>
    public bool HadMaidenOfDeath { get; init; }

    /// <summary>Whether the Matriarch Macabre self-buff took part in this window.</summary>
    public bool HadMatriarchMacabre { get; init; }

    /// <summary>Whether both self-buffs stood at the same time at some point in the window.</summary>
    public bool Overlapped { get; init; }

    /// <summary>Every player cast inside the window, in encounter order.</summary>
    public IReadOnlyList<MaidenWindowCast> Casts { get; init; } = [];

    /// <summary>Queen's Fang and Arachnid Assault casts inside the window.</summary>
    public int ScoredFinisherCasts { get; init; }

    /// <summary>Combo points carried into the window's Queen's Fang and Arachnid Assault casts.</summary>
    public int FinisherComboPointsSpent { get; init; }

    /// <summary>Backstab, Widow's Bite and Skittering Blades casts inside the window.</summary>
    public int GeneratorCasts { get; init; }

    /// <summary>Final Stratagem or Macabre Stratagem casts inside the window.</summary>
    public int ResetCasts { get; init; }

    /// <summary>Whether a reset was cast inside the window.</summary>
    public bool ResetCast => ResetCasts > 0;

    /// <summary>
    /// Energy carried into the last cast inside the window, or <c>null</c> when no cast there reported an
    /// Energy snapshot. A cast's snapshot is the amount available before it resolves, so this is the
    /// Energy the window's final cast went out on rather than the balance when the buff fell off.
    /// </summary>
    public int? EnergyAtClose { get; init; }
}

/// <summary>
/// Measures Mara's burst windows and the discipline between them. A window opens on the Maiden of
/// Death or Matriarch Macabre self-buff landing on the player and closes when neither stands any
/// longer, so a Matriarch Macabre cast during a running Maiden of Death produces one overlapped
/// window rather than two. A window still open at pull end is capped at <see cref="Analyzer.Pull"/>'s
/// end time, because a buff whose removal is never logged still cannot outlive the pull.
/// <para>
/// Each window records what was pressed inside it: the scored finishers (Queen's Fang, Arachnid
/// Assault) and the combo points they carried, the generators, the resets (Final Stratagem or its
/// Macabre Stratagem replacement), and the Energy the last cast went out on. Between windows, every
/// Maiden of Death recast is measured against the ability's static recharge read from the spell
/// registry, so the interval beyond that recharge is time the charge sat ready.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class MaidenOfDeathWindowAnalyzer : Analyzer
{
    /// <summary>Maiden of Death's static recharge in milliseconds, taken from the spell registry.</summary>
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

    private IReadOnlyList<MaidenOfDeathWindow>? _windows;

    /// <summary>Every burst window on the pull, in encounter order.</summary>
    public IReadOnlyList<MaidenOfDeathWindow> Windows => _windows ??= Build();

    public int WindowCount => Windows.Count;

    /// <summary>Windows where both self-buffs stood at the same time.</summary>
    public int OverlappedWindows => Windows.Count(window => window.Overlapped);

    /// <summary>Windows containing a Final Stratagem or Macabre Stratagem cast.</summary>
    public int WindowsWithReset => Windows.Count(window => window.ResetCast);

    /// <summary>Total time the player spent inside a burst window, in milliseconds.</summary>
    public int TotalWindowMs => Windows.Sum(window => window.DurationMs);

    /// <summary>Queen's Fang and Arachnid Assault casts made inside a burst window.</summary>
    public int ScoredFinishersInWindows => Windows.Sum(window => window.ScoredFinisherCasts);

    /// <summary>Combo points carried into the finishers cast inside a burst window.</summary>
    public int ComboPointsSpentInWindows => Windows.Sum(window => window.FinisherComboPointsSpent);

    /// <summary>Final Stratagem and Macabre Stratagem casts made inside a burst window.</summary>
    public int ResetCastsInWindows => Windows.Sum(window => window.ResetCasts);

    /// <summary>Scored finishers per window, or zero when no window opened.</summary>
    public double AverageFinishersPerWindow =>
        Windows.Count == 0 ? 0d : (double)ScoredFinishersInWindows / Windows.Count;

    public int MaidenOfDeathCasts { get; private set; }

    public int MatriarchMacabreCasts { get; private set; }

    /// <summary>Final Stratagem and Macabre Stratagem casts anywhere on the pull.</summary>
    public int ResetCasts { get; private set; }

    /// <summary>Every Maiden of Death cast after the first, with the time its charge sat ready.</summary>
    public IReadOnlyList<MaidenOfDeathRecast> MaidenOfDeathRecasts => _recasts;

    /// <summary>Total time a ready Maiden of Death charge went unspent between recasts, in milliseconds.</summary>
    public int TotalHeldMs => _recasts.Sum(recast => recast.HeldMs);

    /// <summary>Held time per recast, or zero when Maiden of Death was cast at most once.</summary>
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

    /// <summary>A burst window while it is still being accumulated from the buff stream.</summary>
    private sealed class BuffSpan(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int ClosedAt { get; set; } = openedAt;
        public bool HadMaidenOfDeath { get; set; }
        public bool HadMatriarchMacabre { get; set; }
        public bool Overlapped { get; set; }
    }
}
