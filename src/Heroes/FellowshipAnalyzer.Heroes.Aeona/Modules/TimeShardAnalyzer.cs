using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>What became of one Continuum Shift window.</summary>
public enum ContinuumShiftOutcome
{
    /// <summary>Consumed by a Time Shard cast.</summary>
    TimeShard,

    /// <summary>Removed with no Time Shard cast beside it.</summary>
    Lost,

    /// <summary>Still active when the pull ended.</summary>
    OpenAtPullEnd,
}

/// <summary>
/// One Continuum Shift window on the player and the Time Shard that spent it. A removal is credited to
/// the nearest completed Time Shard cast within
/// <see cref="TimeShardAnalyzer.ContinuumShiftPairingMs"/> that no earlier window has claimed.
/// </summary>
/// <param name="Start">When the window opened.</param>
/// <param name="End">When the window closed, or the pull's end time for a window that never closed.</param>
/// <param name="Outcome">What the window was spent on.</param>
/// <param name="ConsumedByTimestamp">When the consuming cast completed, or <see langword="null"/> when no Time Shard consumed it.</param>
public sealed record ContinuumShiftWindow(
    int Start,
    int End,
    ContinuumShiftOutcome Outcome,
    int? ConsumedByTimestamp)
{
    /// <summary>How long the window stayed open.</summary>
    public int DurationMs => End - Start;
}

/// <summary>
/// One Time Shard cast: the enemy it hit and that enemy's Unfolding Doom, the Continuum Shift window it
/// consumed, The Vehement and Martial Initiative around it, the Twilight Skybolt that preceded it, the
/// Chrona held at the cast, and the damage attributed to it.
/// </summary>
public sealed record TimeShardCast
{
    /// <summary>When the cast completed.</summary>
    public required int Timestamp { get; init; }

    /// <summary>
    /// The enemy the cast hit, taken from the damage it produced and falling back to the target the
    /// cast event named. Reads <see cref="UnitKey.ActorId"/> 0 when neither is available.
    /// </summary>
    public required UnitKey Target { get; init; }

    /// <summary>Whether the player's Unfolding Doom was active on <see cref="Target"/> at the cast.</summary>
    public required bool TargetDebuffed { get; init; }

    /// <summary>Whether the cast consumed a Continuum Shift window.</summary>
    public required bool Empowered { get; init; }

    /// <summary>When the consumed Continuum Shift window opened, or <see langword="null"/> for a cast that consumed none.</summary>
    public required int? ContinuumShiftStart { get; init; }

    /// <summary>The Vehement stacks the player held at the cast.</summary>
    public required int VehementStacksAtCast { get; init; }

    /// <summary>The Vehement stacks removed inside the cast's damage window.</summary>
    public required int VehementStacksConsumed { get; init; }

    /// <summary>The Vehement's Disdain damage inside the cast's damage window.</summary>
    public required long VehementDisdainDamage { get; init; }

    /// <summary>
    /// Whether Martial Initiative was active when the cast's damage was dealt, read at the first hit
    /// attributed to it and at the cast itself when no hit was attributed.
    /// </summary>
    public required bool MartialInitiativeActive { get; init; }

    /// <summary>
    /// Milliseconds from the most recent Twilight Skybolt cast to this cast, or <see langword="null"/>
    /// when no Twilight Skybolt was cast earlier in the pull.
    /// </summary>
    public required int? SkyboltLeadMs { get; init; }

    /// <summary>
    /// Whether a Twilight Skybolt was cast at or after <see cref="ContinuumShiftStart"/> and before this
    /// cast.
    /// </summary>
    public required bool SkyboltBeforeCast { get; init; }

    /// <summary>The Chrona the player held at the cast.</summary>
    public required int? ChronaAtCast { get; init; }

    /// <summary>Whether the Chrona at the cast was above half the maximum.</summary>
    public required bool? AboveChronaThreshold { get; init; }

    /// <summary>Chrona lost at the maximum inside the cast's damage window.</summary>
    public required int ChronaOvercap { get; init; }

    /// <summary>The Time Shard damage from the hits inside the cast's damage window.</summary>
    public required long Damage { get; init; }

    /// <summary>Time Shard hits attributed to this cast.</summary>
    public required int Hits { get; init; }

    /// <summary>Time Shard critical hits attributed to this cast.</summary>
    public required int CriticalHits { get; init; }

    /// <summary>Whether The Vehement's Disdain erupted inside the cast's damage window.</summary>
    public bool VehementErupted => VehementDisdainDamage > 0;

    /// <summary>The Time Shard damage plus the Vehement's Disdain damage attributed to this cast.</summary>
    public long TotalDamage => Damage + VehementDisdainDamage;
}

/// <summary>
/// Time Shard's cast quality over one pull: whether each cast had an Unfolding Doom target, which casts
/// consumed Continuum Shift, what the empowered casts were paired with, and the damage each produced.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<ChronaTracker>]
public sealed partial class TimeShardAnalyzer : Analyzer
{
    /// <summary>The blessing the empowered Time Shard is meant to erupt.</summary>
    public const string VehementBlessing = "The Vehement";

    /// <summary>
    /// How long after a Time Shard cast its own damage and the Vehement's Disdain damage beside it are
    /// attributed to that cast. A hit is filed against the most recent Time Shard cast within this span.
    /// </summary>
    public const int DamageWindowMs = 1500;

    /// <summary>
    /// How far a Continuum Shift removal may sit from the cast that consumed it and still pair with it.
    /// </summary>
    public const int ContinuumShiftPairingMs = 2500;

    private readonly List<CastEvent> _casts = [];
    private readonly List<int> _skyboltCasts = [];
    private readonly List<DamageEvent> _damage = [];
    private readonly List<DamageEvent> _vehementDisdainDamage = [];
    private readonly List<(int Timestamp, int Stacks)> _vehementStacks = [];
    private readonly List<(int Start, int End)> _continuumShiftClosed = [];
    private readonly List<(int Start, int? End)> _martialInitiative = [];
    private readonly Dictionary<UnitKey, List<(int Start, int? End)>> _unfoldingDoom = [];

    private int? _openContinuumShift;
    private Evaluation? _evaluation;

    /// <summary>Half the maximum Chrona.</summary>
    public int ChronaThreshold => ChronaTracker.MaxOf(ResourceTypes.Primary) / 2;

    /// <summary>Every Time Shard cast in the pull, in cast order.</summary>
    public IReadOnlyList<TimeShardCast> Casts => Evaluated.Casts;

    /// <summary>How many times Time Shard was cast during the pull.</summary>
    public int CastCount => Casts.Count;

    /// <summary>Casts whose target did not have the player's Unfolding Doom active.</summary>
    public int CastsWithoutUnfoldingDoom => Casts.Count(cast => !cast.TargetDebuffed);

    /// <summary>Share of casts (0-1) whose target did not have the player's Unfolding Doom active.</summary>
    public double CastsWithoutUnfoldingDoomShare =>
        CastCount == 0 ? 0 : (double)CastsWithoutUnfoldingDoom / CastCount;

    /// <summary>Casts that consumed a Continuum Shift window.</summary>
    public int EmpoweredCasts => Casts.Count(cast => cast.Empowered);

    /// <summary>Time Shard damage across every cast in the pull.</summary>
    public long TotalDamage => Casts.Sum(cast => cast.Damage);

    /// <summary>The Vehement's Disdain damage attributed to Time Shard casts across the pull.</summary>
    public long TotalVehementDisdainDamage => Casts.Sum(cast => cast.VehementDisdainDamage);

    /// <summary>Average Time Shard damage plus attributed Vehement's Disdain damage per cast.</summary>
    public double AverageDamagePerCast =>
        CastCount == 0 ? 0 : Casts.Sum(cast => (double)cast.TotalDamage) / CastCount;

    /// <summary>Every Continuum Shift window in the pull, in the order they opened.</summary>
    public IReadOnlyList<ContinuumShiftWindow> ContinuumShiftWindows => Evaluated.Windows;

    /// <summary>Continuum Shift windows the pull opened.</summary>
    public int ContinuumShiftProcs => ContinuumShiftWindows.Count;

    /// <summary>Continuum Shift windows that closed during the pull.</summary>
    public int ContinuumShiftClosedWindows =>
        ContinuumShiftWindows.Count(window => window.Outcome != ContinuumShiftOutcome.OpenAtPullEnd);

    /// <summary>Continuum Shift windows a Time Shard cast consumed.</summary>
    public int ContinuumShiftSpentOnTimeShard =>
        ContinuumShiftWindows.Count(window => window.Outcome == ContinuumShiftOutcome.TimeShard);

    /// <summary>Continuum Shift windows removed with no Time Shard cast consuming them.</summary>
    public int ContinuumShiftLost =>
        ContinuumShiftWindows.Count(window => window.Outcome == ContinuumShiftOutcome.Lost);

    /// <summary>Share of closed Continuum Shift windows (0-1) no Time Shard cast consumed.</summary>
    public double ContinuumShiftLostShare =>
        ContinuumShiftClosedWindows == 0 ? 0 : (double)ContinuumShiftLost / ContinuumShiftClosedWindows;

    /// <summary>Whether the player selected the Continuum Shift talent.</summary>
    public bool ContinuumShiftTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.ContinuumShift);

    /// <summary>Whether the player selected the Synchronicity talent.</summary>
    public bool SynchronicityTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.Synchronicity);

    /// <summary>Whether the build has The Vehement blessing.</summary>
    public bool VehementEquipped => Owner.SelectedCombatant.BlessingLevel(VehementBlessing) > 0;

    /// <summary>Whether the build has the Martial Initiative trait.</summary>
    public bool MartialInitiativeTaken =>
        Owner.SelectedCombatant.TraitRank(CoreItems.MartialInitiativeTrait.FSLID) > 0;

    /// <summary>Empowered casts The Vehement's Disdain erupted on.</summary>
    public int VehementPairings => Casts.Count(cast => cast.Empowered && cast.VehementErupted);

    /// <summary>Empowered casts above the Chrona threshold.</summary>
    public int SynchronicityPairings =>
        Casts.Count(cast => cast.Empowered && cast.AboveChronaThreshold == true);

    /// <summary>Empowered casts with the Chrona held at the cast.</summary>
    public int EmpoweredCastsWithChrona =>
        Casts.Count(cast => cast.Empowered && cast.AboveChronaThreshold is not null);

    /// <summary>Empowered casts whose damage was dealt with Martial Initiative active.</summary>
    public int MartialInitiativePairings =>
        Casts.Count(cast => cast.Empowered && cast.MartialInitiativeActive);

    /// <summary>Empowered casts a Twilight Skybolt preceded inside the Continuum Shift window.</summary>
    public int SkyboltPairings => Casts.Count(cast => cast.Empowered && cast.SkyboltBeforeCast);

    /// <summary>Twilight Skybolt casts during the pull, in cast order.</summary>
    public IReadOnlyList<int> SkyboltCasts => _skyboltCasts;

    private Evaluation Evaluated => _evaluation ??= Evaluate();

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.TimeShard))]
    private void OnTimeShardCast(CastEvent e) => _casts.Add(e);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(CoreItems.TwilightSkybolt))]
    private void OnSkyboltCast(CastEvent e) => _skyboltCasts.Add(e.Timestamp);

    [On<DamageEvent>(By = Actor.Player, Spells = [nameof(Spells.TimeShard), nameof(Spells.TimeShardDamage)])]
    private void OnTimeShardDamage(DamageEvent e) => _damage.Add(e);

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(CoreItems.TheVehementsDisdain))]
    private void OnVehementDisdainDamage(DamageEvent e) => _vehementDisdainDamage.Add(e);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoomDebuff))]
    private void OnUnfoldingDoomApply(ApplyDebuffEvent e) => OpenUnfoldingDoom(e, e.Timestamp);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoomDebuff))]
    private void OnUnfoldingDoomRefresh(RefreshDebuffEvent e) => OpenUnfoldingDoom(e, e.Timestamp);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.UnfoldingDoomDebuff))]
    private void OnUnfoldingDoomRemove(RemoveDebuffEvent e)
    {
        if (!_unfoldingDoom.TryGetValue(KeyOf(e), out var windows))
            return;

        for (var i = windows.Count - 1; i >= 0; i--)
        {
            if (windows[i].End is not null) continue;

            windows[i] = (windows[i].Start, e.Timestamp);
            return;
        }
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnContinuumShiftApply(ApplyBuffEvent e) => OpenContinuumShift(e.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnContinuumShiftRefresh(RefreshBuffEvent e) => OpenContinuumShift(e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnContinuumShiftRemove(RemoveBuffEvent e)
    {
        if (_openContinuumShift is not { } start) return;

        _openContinuumShift = null;
        _continuumShiftClosed.Add((start, e.Timestamp));
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(CoreItems.MartialInitiative))]
    private void OnMartialInitiativeApply(ApplyBuffEvent e) => OpenMartialInitiative(e.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(CoreItems.MartialInitiative))]
    private void OnMartialInitiativeRefresh(RefreshBuffEvent e) => OpenMartialInitiative(e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(CoreItems.MartialInitiative))]
    private void OnMartialInitiativeRemove(RemoveBuffEvent e)
    {
        for (var i = _martialInitiative.Count - 1; i >= 0; i--)
        {
            if (_martialInitiative[i].End is not null) continue;

            _martialInitiative[i] = (_martialInitiative[i].Start, e.Timestamp);
            return;
        }
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(CoreItems.TheVehement))]
    private void OnVehementApply(ApplyBuffEvent e) => RecordVehementStacks(e.Timestamp, 1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(CoreItems.TheVehement))]
    private void OnVehementStackApply(ApplyBuffStackEvent e) => RecordVehementStacks(e.Timestamp, e.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(CoreItems.TheVehement))]
    private void OnVehementStackRemove(RemoveBuffStackEvent e) => RecordVehementStacks(e.Timestamp, e.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(CoreItems.TheVehement))]
    private void OnVehementRemove(RemoveBuffEvent e) => RecordVehementStacks(e.Timestamp, 0);

    private void OpenUnfoldingDoom(IHasTargetWithInstanceEvent target, int timestamp)
    {
        var key = KeyOf(target);
        if (!_unfoldingDoom.TryGetValue(key, out var windows))
            _unfoldingDoom[key] = windows = [];

        foreach (var window in windows)
        {
            if (window.End is null) return;
        }

        windows.Add((timestamp, null));
    }

    private void OpenContinuumShift(int timestamp) => _openContinuumShift ??= timestamp;

    private void OpenMartialInitiative(int timestamp)
    {
        foreach (var window in _martialInitiative)
        {
            if (window.End is null) return;
        }

        _martialInitiative.Add((timestamp, null));
    }

    private void RecordVehementStacks(int timestamp, int stacks) =>
        _vehementStacks.Add((timestamp, Math.Max(0, stacks)));

    private static UnitKey KeyOf(IHasTargetWithInstanceEvent target) =>
        new(target.TargetId, target.TargetInstance ?? 0);

    private Evaluation Evaluate()
    {
        var windows = BuildContinuumShiftWindows(out var empoweredWindowStarts);
        var casts = new List<TimeShardCast>(_casts.Count);
        var threshold = ChronaThreshold;

        foreach (var cast in _casts)
        {
            var hits = HitsFor(cast.Timestamp);
            var target = ResolveTarget(cast, hits);
            var continuumShiftStart = empoweredWindowStarts.GetValueOrDefault(cast.Timestamp, -1);
            var empowered = continuumShiftStart >= 0;
            var chrona = ChronaTracker.SnapshotAt(ResourceTypes.Primary, cast.Timestamp);

            casts.Add(new TimeShardCast
            {
                Timestamp = cast.Timestamp,
                Target = target,
                TargetDebuffed = UnfoldingDoomActive(target, cast.Timestamp),
                Empowered = empowered,
                ContinuumShiftStart = empowered ? continuumShiftStart : null,
                VehementStacksAtCast = StacksAt(cast.Timestamp),
                VehementStacksConsumed = VehementStacksRemovedNear(cast.Timestamp),
                VehementDisdainDamage = VehementDisdainDamageNear(cast.Timestamp),
                MartialInitiativeActive =
                    ActiveAt(_martialInitiative, hits.Count > 0 ? hits[0].Timestamp : cast.Timestamp),
                SkyboltLeadMs = SkyboltLead(cast.Timestamp),
                SkyboltBeforeCast = empowered && SkyboltInside(continuumShiftStart, cast.Timestamp),
                ChronaAtCast = chrona,
                AboveChronaThreshold = chrona is { } held ? held > threshold : null,
                ChronaOvercap = ChronaTracker.OvercapBetween(
                    ResourceTypes.Primary,
                    cast.Timestamp,
                    cast.Timestamp + DamageWindowMs),
                Damage = hits.Sum(hit => hit.Amount),
                Hits = hits.Count,
                CriticalHits = hits.Count(hit => hit.IsCritical),
            });
        }

        return new Evaluation(casts, windows);
    }

    private List<ContinuumShiftWindow> BuildContinuumShiftWindows(out Dictionary<int, int> empoweredWindowStarts)
    {
        empoweredWindowStarts = [];

        var windows = new List<ContinuumShiftWindow>(_continuumShiftClosed.Count + 1);
        var claimed = new HashSet<int>();

        foreach (var (start, end) in _continuumShiftClosed)
        {
            if (NearestCast(_casts.Select(cast => cast.Timestamp), end, claimed) is { } shard)
            {
                claimed.Add(shard);
                empoweredWindowStarts[shard] = start;
                windows.Add(new ContinuumShiftWindow(start, end, ContinuumShiftOutcome.TimeShard, shard));
                continue;
            }

            windows.Add(new ContinuumShiftWindow(start, end, ContinuumShiftOutcome.Lost, null));
        }

        if (_openContinuumShift is { } openStart)
            windows.Add(new ContinuumShiftWindow(openStart, Pull.EndTime, ContinuumShiftOutcome.OpenAtPullEnd, null));

        windows.Sort((left, right) => left.Start.CompareTo(right.Start));
        return windows;
    }

    private static int? NearestCast(IEnumerable<int> candidates, int removal, HashSet<int> claimed)
    {
        int? nearest = null;
        foreach (var candidate in candidates)
        {
            if (claimed.Contains(candidate)) continue;

            var distance = Distance(candidate, removal);
            if (distance > ContinuumShiftPairingMs) continue;
            if (nearest is { } current && Distance(current, removal) <= distance) continue;

            nearest = candidate;
        }

        return nearest;
    }

    private static int Distance(int left, int right) => Math.Abs(left - right);

    private static UnitKey ResolveTarget(CastEvent cast, List<DamageEvent> hits)
    {
        if (hits.Count > 0) return new UnitKey(hits[0].TargetId, hits[0].TargetInstance ?? 0);

        return cast.TargetId > 0 ? new UnitKey(cast.TargetId, cast.TargetInstance ?? 0) : new UnitKey(0, 0);
    }

    private List<DamageEvent> HitsFor(int castTimestamp)
    {
        var hits = new List<DamageEvent>();
        foreach (var hit in _damage)
        {
            if (hit.Timestamp < castTimestamp || hit.Timestamp > castTimestamp + DamageWindowMs) continue;
            if (MostRecentCastAtOrBefore(hit.Timestamp) != castTimestamp) continue;

            hits.Add(hit);
        }

        return hits;
    }

    private long VehementDisdainDamageNear(int castTimestamp)
    {
        long total = 0;
        foreach (var hit in _vehementDisdainDamage)
        {
            if (hit.Timestamp < castTimestamp || hit.Timestamp > castTimestamp + DamageWindowMs) continue;
            if (MostRecentCastAtOrBefore(hit.Timestamp) != castTimestamp) continue;

            total += hit.Amount;
        }

        return total;
    }

    private int MostRecentCastAtOrBefore(int timestamp)
    {
        var latest = int.MinValue;
        foreach (var cast in _casts)
        {
            if (cast.Timestamp > timestamp) break;
            latest = cast.Timestamp;
        }

        return latest;
    }

    private bool UnfoldingDoomActive(UnitKey target, int timestamp)
    {
        if (!_unfoldingDoom.TryGetValue(target, out var windows)) return false;

        foreach (var (start, end) in windows)
        {
            if (start <= timestamp && (end ?? Pull.EndTime) >= timestamp) return true;
        }

        return false;
    }

    private static bool ActiveAt(List<(int Start, int? End)> windows, int timestamp)
    {
        foreach (var (start, end) in windows)
        {
            if (start <= timestamp && (end ?? int.MaxValue) >= timestamp) return true;
        }

        return false;
    }

    private int StacksAt(int timestamp)
    {
        var stacks = 0;
        foreach (var (sampleTime, sampleStacks) in _vehementStacks)
        {
            if (sampleTime > timestamp) break;
            stacks = sampleStacks;
        }

        return stacks;
    }

    private int VehementStacksRemovedNear(int castTimestamp)
    {
        var removed = 0;
        for (var i = 1; i < _vehementStacks.Count; i++)
        {
            var (timestamp, stacks) = _vehementStacks[i];
            if (timestamp < castTimestamp || timestamp > castTimestamp + DamageWindowMs) continue;

            var previous = _vehementStacks[i - 1].Stacks;
            if (stacks < previous)
                removed += previous - stacks;
        }

        return removed;
    }

    private int? SkyboltLead(int castTimestamp)
    {
        int? lead = null;
        foreach (var skybolt in _skyboltCasts)
        {
            if (skybolt > castTimestamp) break;
            lead = castTimestamp - skybolt;
        }

        return lead;
    }

    private bool SkyboltInside(int windowStart, int castTimestamp)
    {
        foreach (var skybolt in _skyboltCasts)
        {
            if (skybolt >= windowStart && skybolt < castTimestamp) return true;
        }

        return false;
    }

    private sealed record Evaluation(List<TimeShardCast> Casts, List<ContinuumShiftWindow> Windows);
}
