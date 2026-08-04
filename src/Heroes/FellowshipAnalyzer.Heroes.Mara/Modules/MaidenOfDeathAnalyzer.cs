using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Utility;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>The part a cast plays in the combo point cycle inside a Maiden of Death window.</summary>
public enum MaidenCastRole
{
    /// <summary>An ability that generates combo points, filling the pool outright while Maiden of Death is active.</summary>
    Generator,

    /// <summary>An ability that consumes the combo points currently held.</summary>
    Spender,

    /// <summary>An ability that neither generates nor consumes combo points.</summary>
    Other,
}

/// <summary>One cast made inside a Maiden of Death window.</summary>
/// <param name="Timestamp">When the cast was made.</param>
/// <param name="AbilityId">The ability cast.</param>
/// <param name="Role">The part the cast plays in the combo point cycle.</param>
/// <param name="ComboPointsBefore">
/// Combo points held going into the cast, where the log attached a resource snapshot.
/// </param>
/// <param name="WastedGeneration">
/// Whether this generator followed another generator with no spender between them, so the combo points the
/// earlier generator produced were overwritten before they were spent.
/// </param>
public sealed record MaidenWindowCast(
    int Timestamp,
    int AbilityId,
    MaidenCastRole Role,
    int? ComboPointsBefore,
    bool WastedGeneration);

/// <summary>A stretch inside a Maiden of Death window with no ability going out.</summary>
/// <param name="StartTimestamp">When the idle stretch began.</param>
/// <param name="DurationMs">How long it lasted.</param>
public sealed record MaidenWindowGap(int StartTimestamp, int DurationMs);

/// <summary>A Maiden of Death cast measured against the recharge since the previous one.</summary>
/// <param name="Timestamp">When the recast was made.</param>
/// <param name="GapMs">Time since the previous Maiden of Death cast.</param>
/// <param name="HeldMs">How much of that gap ran past the recharge, so a ready charge went unspent.</param>
public sealed record MaidenOfDeathRecast(int Timestamp, int GapMs, int HeldMs);

/// <summary>
/// One Maiden of Death window, from the buff landing to its removal, with the cast sequence inside it.
/// </summary>
public sealed record MaidenOfDeathWindow
{
    /// <summary>When the Maiden of Death buff landed.</summary>
    public required int OpenedAt { get; init; }

    /// <summary>When the Maiden of Death buff came off, or the pull ended.</summary>
    public required int ClosedAt { get; init; }

    /// <summary>How long the buff was active.</summary>
    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    /// <summary>Every cast made while the buff was active, in order.</summary>
    public IReadOnlyList<MaidenWindowCast> Casts { get; init; } = [];

    /// <summary>
    /// Idle stretches inside the window that ran to at least
    /// <see cref="MaidenOfDeathAnalyzer.MinimumGapMs"/>.
    /// </summary>
    public IReadOnlyList<MaidenWindowGap> Gaps { get; init; } = [];

    /// <summary>Combo point generating casts made inside the window.</summary>
    public int GeneratorCasts { get; init; }

    /// <summary>Combo point spending casts made inside the window.</summary>
    public int SpenderCasts { get; init; }

    /// <summary>
    /// Generators that landed on a pool with nowhere to put the generation: one following another generator
    /// with no spender between them, or an opening generator on a window that began at
    /// <see cref="MaidenOfDeathAnalyzer.OpeningComboPointLimit"/> combo points or more.
    /// </summary>
    public int WastedGeneratorCasts { get; init; }

    /// <summary>
    /// Combo points held when the window opened, read from the combo point tracker. Null when the tracker
    /// recorded nothing at or before that instant.
    /// </summary>
    public int? ComboPointsAtOpen { get; init; }

    /// <summary>
    /// Combo points thrown away by <see cref="WastedGeneratorCasts"/>, at the full pool a generator produces
    /// while Maiden of Death is active.
    /// </summary>
    public int ComboPointsWasted => WastedGeneratorCasts * MaidenOfDeathAnalyzer.ComboPointsPerGenerator;

    /// <summary>Total idle time inside the window.</summary>
    public int IdleMs { get; init; }

    /// <summary>How many idle stretches the window contained.</summary>
    public int GapCount => Gaps.Count;

    /// <summary>The longest single idle stretch inside the window.</summary>
    public int LongestGapMs => Gaps.Count == 0 ? 0 : Gaps.Max(gap => gap.DurationMs);

    /// <summary>
    /// Casts the idle time had room for, at one global cooldown each. Counted against the global cooldown
    /// rather than against any other window.
    /// </summary>
    public int LostCasts => IdleMs / MaidenOfDeathAnalyzer.StandardGcdMs;

    /// <summary>Combo points carried into the spenders cast inside the window, where the log recorded them.</summary>
    public int ComboPointsSpent { get; init; }

    /// <summary>Energy held going into the last cast inside the window, where the log recorded it.</summary>
    public int? EnergyAtClose { get; init; }

    /// <summary>Whether the window ran with no wasted generator and no idle stretch.</summary>
    public bool CleanCycle => WastedGeneratorCasts == 0 && Gaps.Count == 0;
}

/// <summary>
/// Measures how the combo point cycle ran inside each Maiden of Death window. While the buff is active a
/// generator fills the combo point pool outright and a spender consumes whatever is held, so the window
/// rewards strict alternation. Two mistakes cost throughput: a second generator pressed before a spender,
/// which overwrites combo points that were never spent, and idle time the window had room to cast into.
/// Matriarch Macabre takes no part in this measurement; it is assessed on its own terms by
/// <see cref="MatriarchMacabreAnalyzer"/>.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<EnergyComboPointTracker>]
public sealed partial class MaidenOfDeathAnalyzer : Analyzer
{
    /// <summary>Combo points a generator produces while Maiden of Death is active.</summary>
    public const int ComboPointsPerGenerator = 6;

    /// <summary>
    /// Combo points held at a window's open that make an opening generator a doubled builder. A generator
    /// grants a full pool outright, so entering at this many or more leaves most of that generation nowhere
    /// to land.
    /// </summary>
    public const int OpeningComboPointLimit = 4;

    /// <summary>
    /// The shortest stretch counted as idle. Anything under this is cast latency rather than a mistake, and
    /// matches the threshold Rime's downtime measurement uses.
    /// </summary>
    public const int MinimumGapMs = 500;

    /// <summary>The global cooldown a cast is taken to occupy when the log attached no global cooldown to it.</summary>
    public const int StandardGcdMs = 1500;

    /// <summary>The damage increase Maiden of Death grants while it is active.</summary>
    public const double DamageIncrease = 0.20;

    /// <summary>
    /// The damage increase Maiden of Death grants under Maiden's Doom against a target at or below
    /// <see cref="LowHealthThreshold"/> health. It replaces <see cref="DamageIncrease"/> rather than
    /// compounding with it.
    /// </summary>
    public const double LowHealthDamageIncrease = 0.40;

    /// <summary>The share of maximum health at or below which Maiden's Doom raises the increase.</summary>
    public const double LowHealthThreshold = 0.30;

    /// <summary>Maiden of Death's recharge, taken from the spell registry.</summary>
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

    /// <summary>Every Maiden of Death window on this pull.</summary>
    public IReadOnlyList<MaidenOfDeathWindow> Windows => field ??= Build();

    /// <summary>How many Maiden of Death windows the pull contained.</summary>
    public int WindowCount => Windows.Count;

    /// <summary>Windows that ran with no wasted generator and no idle stretch.</summary>
    public int CleanWindows => Windows.Count(window => window.CleanCycle);

    /// <summary>Combo point spending casts made inside a window.</summary>
    public int SpendersInWindows => Windows.Sum(window => window.SpenderCasts);

    /// <summary>Combo point generating casts made inside a window.</summary>
    public int GeneratorsInWindows => Windows.Sum(window => window.GeneratorCasts);

    /// <summary>Generators that overwrote combo points a previous generator had produced.</summary>
    public int WastedGeneratorCasts => Windows.Sum(window => window.WastedGeneratorCasts);

    /// <summary>Combo points thrown away by generators pressed back to back inside a window.</summary>
    public int ComboPointsWasted => Windows.Sum(window => window.ComboPointsWasted);

    /// <summary>Total idle time across every window.</summary>
    public int IdleMs => Windows.Sum(window => window.IdleMs);

    /// <summary>Idle stretches across every window.</summary>
    public int GapCount => Windows.Sum(window => window.GapCount);

    /// <summary>Casts the idle time across every window had room for.</summary>
    public int LostCasts => Windows.Sum(window => window.LostCasts);

    /// <summary>Combo points carried into the spenders cast inside a window.</summary>
    public int ComboPointsSpentInWindows => Windows.Sum(window => window.ComboPointsSpent);

    /// <summary>Average number of spenders each window carried.</summary>
    public double AverageSpendersPerWindow =>
        Windows.Count == 0 ? 0d : (double)SpendersInWindows / Windows.Count;

    /// <summary>Maiden of Death casts made on this pull.</summary>
    public int MaidenOfDeathCasts { get; private set; }

    /// <summary>Every Maiden of Death recast measured against the recharge since the previous cast.</summary>
    public IReadOnlyList<MaidenOfDeathRecast> MaidenOfDeathRecasts => _recasts;

    /// <summary>Total time a ready Maiden of Death charge went unspent between casts.</summary>
    public int TotalHeldMs => _recasts.Sum(recast => recast.HeldMs);

    /// <summary>Average time a ready charge went unspent per recast.</summary>
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

    /// <summary>Whether the player took Maiden's Doom, which raises the increase against a low-health target.</summary>
    public bool HasMaidensDoom => Owner.SelectedCombatant.HasTalent(MaraTalents.MaidensDoom);

    /// <summary>
    /// The damage Maiden of Death's increase accounts for across the pull's windows.
    /// <para>
    /// Measured per hit against Maiden of Death's own multiplier. Matriarch Macabre can be active over
    /// the same hit, and each buff is measured as though the other still applied, so this figure and
    /// Matriarch Macabre's overlap. Read them separately; never sum them, and never render either as a
    /// share of the pull's damage.
    /// </para>
    /// </summary>
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
