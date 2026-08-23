using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Utility;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public enum MaidenCastRole
{
    Generator,

    Spender,

    Other,
}

public sealed record MaidenWindowCast(
    int Timestamp,
    int AbilityId,
    MaidenCastRole Role,
    int? ComboPointsBefore,
    bool WastedGeneration);

public sealed record MaidenWindowGap(int StartTimestamp, int DurationMs);

public sealed record MaidenOfDeathRecast(int Timestamp, int GapMs, int HeldMs);

public sealed record MaidenOfDeathWindow
{
    public required int OpenedAt { get; init; }

    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    public List<MaidenWindowCast> Casts { get; init; } = [];

    public List<MaidenWindowGap> Gaps { get; init; } = [];

    public int GeneratorCasts { get; init; }

    public int SpenderCasts { get; init; }

    public int WastedGeneratorCasts { get; init; }

    public int? ComboPointsAtOpen { get; init; }

    public int ComboPointsWasted => WastedGeneratorCasts * MaidenOfDeathAnalyzer.ComboPointsPerGenerator;

    public int IdleMs { get; init; }

    public int GapCount => Gaps.Count;

    public int LongestGapMs => Gaps.Count == 0 ? 0 : Gaps.Max(gap => gap.DurationMs);

    public int LostCasts => IdleMs / MaidenOfDeathAnalyzer.StandardGcdMs;

    public int ComboPointsSpent { get; init; }

    public int? EnergyAtClose { get; init; }

    public bool CleanCycle => WastedGeneratorCasts == 0 && Gaps.Count == 0;
}

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<EnergyComboPointTracker>]
public sealed partial class MaidenOfDeathAnalyzer : Analyzer
{
    public const int ComboPointsPerGenerator = 6;

    public const int OpeningComboPointLimit = 4;

    public const int MinimumGapMs = 500;

    public const int StandardGcdMs = 1500;

    public const double DamageIncrease = 0.20;

    public const double LowHealthDamageIncrease = 0.40;

    public const double LowHealthThreshold = 0.30;

    public static int RechargeMs { get; } = (int)Math.Round(Spells.MaidenOfDeath.Cooldown.GetValueOrDefault() * 1000);

    private static readonly int[] Spenders =
        [Spells.QueenFang.Id, Spells.ArachnidAssault.Id, Spells.HemorrhagingStrike.Id];

    private static readonly int[] Generators =
        [Spells.Backstab.Id, Spells.WidowBite.Id, Spells.SkitteringBlades.Id];

    private readonly List<CastEvent> _casts = [];
    private readonly List<BuffSpan> _spans = [];
    private readonly List<MaidenOfDeathRecast> _recasts = [];

    private BuffSpan? _openSpan;
    private int _previousMaidenCast = -1;

    public List<MaidenOfDeathWindow> Windows => field ??= Build();

    public int WindowCount => Windows.Count;

    public int CleanWindows => Windows.Count(window => window.CleanCycle);

    public int SpendersInWindows => Windows.Sum(window => window.SpenderCasts);

    public int GeneratorsInWindows => Windows.Sum(window => window.GeneratorCasts);

    public int WastedGeneratorCasts => Windows.Sum(window => window.WastedGeneratorCasts);

    public int ComboPointsWasted => Windows.Sum(window => window.ComboPointsWasted);

    public int IdleMs => Windows.Sum(window => window.IdleMs);

    public int GapCount => Windows.Sum(window => window.GapCount);

    public int LostCasts => Windows.Sum(window => window.LostCasts);

    public int ComboPointsSpentInWindows => Windows.Sum(window => window.ComboPointsSpent);

    public double AverageSpendersPerWindow =>
        Windows.Count == 0 ? 0d : (double)SpendersInWindows / Windows.Count;

    public int MaidenOfDeathCasts { get; private set; }

    public List<MaidenOfDeathRecast> MaidenOfDeathRecasts => _recasts;

    public int TotalHeldMs => _recasts.Sum(recast => recast.HeldMs);

    public double AverageHeldMs => _recasts.Count == 0 ? 0d : (double)TotalHeldMs / _recasts.Count;

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        _casts.Add(castEvent);

        if (castEvent.Ability.Id != Spells.MaidenOfDeath.Id)
            return;

        MaidenOfDeathCasts++;

        if (_previousMaidenCast >= 0)
        {
            var gap = castEvent.Timestamp - _previousMaidenCast;
            _recasts.Add(new MaidenOfDeathRecast(castEvent.Timestamp, gap, Held(gap)));
        }

        _previousMaidenCast = castEvent.Timestamp;
    }

    public bool HasMaidensDoom => Owner.SelectedCombatant.HasTalent(MaraTalents.MaidensDoom);

    public long AddedDamage { get; private set; }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamageDealt(DamageEvent damageEvent)
    {
        if (_openSpan is null) return;

        AddedDamage += CombatMath.CalculateEffectiveDamage(damageEvent, IncreaseFor(damageEvent));
    }

    private double IncreaseFor(DamageEvent damageEvent) =>
        HasMaidensDoom && IsLowHealth(damageEvent) ? LowHealthDamageIncrease : DamageIncrease;

    private static bool IsLowHealth(DamageEvent damageEvent) =>
        damageEvent.TargetResources is { MaxHitPoints: > 0 } resources
        && resources.HitPoints / (double)resources.MaxHitPoints <= LowHealthThreshold;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MaidenOfDeathBuff))]
    private void OnBuffApplied(ApplyBuffEvent buffEvent)
    {
        _openSpan ??= new BuffSpan(buffEvent.Timestamp);
        _openSpan.ClosedAt = Math.Max(_openSpan.ClosedAt, buffEvent.Timestamp);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MaidenOfDeathBuff))]
    private void OnBuffRemoved(RemoveBuffEvent buffEvent)
    {
        if (_openSpan is null)
            return;

        _openSpan.ClosedAt = Math.Max(_openSpan.ClosedAt, buffEvent.Timestamp);
        _spans.Add(_openSpan);
        _openSpan = null;
    }

    private static int Held(int gapMs) => RechargeMs <= 0 ? 0 : Math.Max(0, gapMs - RechargeMs);

    private static MaidenCastRole RoleOf(int abilityId) =>
        Array.IndexOf(Generators, abilityId) >= 0 ? MaidenCastRole.Generator
        : Array.IndexOf(Spenders, abilityId) >= 0 ? MaidenCastRole.Spender
        : MaidenCastRole.Other;

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
        var gaps = new List<MaidenWindowGap>();
        var generators = 0;
        var spenders = 0;
        var wasted = 0;
        var comboPointsSpent = 0;
        var idleMs = 0;
        var comboPointsAtOpen = ComboPointsAt(span.OpenedAt);
        var poolFilled = comboPointsAtOpen >= OpeningComboPointLimit;
        var cursor = span.OpenedAt;
        int? energy = null;

        foreach (var cast in _casts)
        {
            if (cast.Timestamp < span.OpenedAt || cast.Timestamp > closedAt)
                continue;

            var idle = cast.Timestamp - cursor;
            if (idle >= MinimumGapMs)
            {
                gaps.Add(new MaidenWindowGap(cursor, idle));
                idleMs += idle;
            }

            cursor = Math.Max(cursor, cast.Timestamp + BusyMs(cast));

            var abilityId = cast.Ability.Id;
            var role = RoleOf(abilityId);
            var resources = cast.SourceResources?.Resources;

            if (FindResource(resources, ResourceTypes.Primary) is { } primary)
                energy = primary.Amount;

            var comboPointsBefore = FindResource(resources, ResourceTypes.Secondary)?.Amount;

            var wastedGeneration = false;
            if (role == MaidenCastRole.Generator)
            {
                generators++;
                wastedGeneration = poolFilled;
                if (wastedGeneration)
                    wasted++;
                poolFilled = true;
            }
            else if (role == MaidenCastRole.Spender)
            {
                spenders++;
                poolFilled = false;
                if (comboPointsBefore is { } held)
                    comboPointsSpent += held;
            }

            casts.Add(new MaidenWindowCast(cast.Timestamp, abilityId, role, comboPointsBefore, wastedGeneration));
        }

        var trailingIdle = closedAt - cursor;
        if (trailingIdle >= MinimumGapMs)
        {
            gaps.Add(new MaidenWindowGap(cursor, trailingIdle));
            idleMs += trailingIdle;
        }

        return new MaidenOfDeathWindow
        {
            OpenedAt = span.OpenedAt,
            ClosedAt = closedAt,
            Casts = casts,
            Gaps = gaps,
            GeneratorCasts = generators,
            SpenderCasts = spenders,
            WastedGeneratorCasts = wasted,
            ComboPointsAtOpen = comboPointsAtOpen,
            IdleMs = idleMs,
            ComboPointsSpent = comboPointsSpent,
            EnergyAtClose = energy,
        };
    }

    private int? ComboPointsAt(int timestamp)
    {
        int? held = null;
        foreach (var resourceEvent in EnergyComboPointTracker.GetResourceEvents(ResourceTypes.Secondary))
        {
            if (resourceEvent.Timestamp > timestamp)
                break;

            held = resourceEvent.CurrentAfter;
        }

        return held;
    }

    private static int BusyMs(CastEvent cast) =>
        cast.GlobalCooldown is { Duration: > 0 } gcd ? gcd.Duration : StandardGcdMs;

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
    }
}
