using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Where Bluey is. There is one blue butterfly and it is always somewhere: parked on an ally by
/// <see cref="Spells.FluttercallProtect"/> or recalled onto Sylvie by
/// <see cref="Spells.FluttercallEmbrace"/>, and the two auras are mutually exclusive - the log removes
/// one in the same millisecond it applies the other.
/// <para>
/// Bluey is fight-lifetime state, not pull state. In the validation report it sat in one place for
/// 26 minutes across ten pulls, so a pull-lifetime reading would see no application event and report
/// nothing at all.
/// </para>
/// <para>
/// A removal with no application under it means Bluey was already placed when logging began; that
/// posting is backdated to the fight's start, which is the earliest moment the log can support.
/// </para>
/// </summary>
public sealed partial class BlueyTracker : EventSubscriber
{
    private readonly List<BlueyPosting> _postings = [];

    private BlueyPosting? _open;

    /// <summary>Every place Bluey sat this parse, in order.</summary>
    public IReadOnlyList<BlueyPosting> Postings => _postings;

    /// <summary>Casts that moved Bluey, counting both the send-out and the recall.</summary>
    public int Reassignments { get; private set; }

    /// <summary>Where Bluey was at <paramref name="timestamp"/>, or <c>null</c> when the log had not placed it yet.</summary>
    public BlueyPosting? PostingAt(int timestamp)
    {
        BlueyPosting? found = null;
        foreach (var posting in _postings)
        {
            if (posting.Start > timestamp) break;
            if (posting.End is null || posting.End > timestamp) found = posting;
        }

        return found;
    }

    /// <summary>
    /// Milliseconds Bluey spent on each ally between <paramref name="start"/> and <paramref name="end"/>,
    /// keyed by the ally's id, longest first.
    /// </summary>
    public IReadOnlyList<(int TargetId, int Ms, bool OnSylvie)> TimeByHolderBetween(int start, int end)
    {
        var byTarget = new Dictionary<int, int>();
        foreach (var posting in _postings)
        {
            var from = Math.Max(posting.Start, start);
            var to = Math.Min(posting.End ?? end, end);
            if (to <= from) continue;

            byTarget[posting.TargetId] = byTarget.GetValueOrDefault(posting.TargetId) + (to - from);
        }

        return
        [
            .. byTarget
                .Select(entry => (entry.Key, entry.Value, entry.Key == PlayerId))
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key)
        ];
    }

    /// <summary>Milliseconds Bluey spent on Sylvie herself between <paramref name="start"/> and <paramref name="end"/>.</summary>
    public int SelfMsBetween(int start, int end) =>
        TimeByHolderBetween(start, end).Where(entry => entry.OnSylvie).Sum(entry => entry.Ms);

    /// <summary>Milliseconds Bluey spent on somebody other than Sylvie between <paramref name="start"/> and <paramref name="end"/>.</summary>
    public int AllyMsBetween(int start, int end) =>
        TimeByHolderBetween(start, end).Where(entry => !entry.OnSylvie).Sum(entry => entry.Ms);

    [On<ApplyBuffEvent>(By = Actor.Player)]
    private void OnApplied(ApplyBuffEvent buffEvent) => Post(buffEvent, buffEvent.TargetId);

    [On<RefreshBuffEvent>(By = Actor.Player)]
    private void OnRefreshed(RefreshBuffEvent buffEvent) => Post(buffEvent, buffEvent.TargetId);

    [On<RemoveBuffEvent>(By = Actor.Player)]
    private void OnRemoved(RemoveBuffEvent buffEvent)
    {
        if (!IsBluey(buffEvent.Ability.Id)) return;

        if (_open is null)
        {
            var priorPosting = new BlueyPosting(buffEvent.TargetId, buffEvent.TargetId == PlayerId, Owner.FightStartTime);
            priorPosting.Close(buffEvent.Timestamp);
            _postings.Insert(0, priorPosting);
            return;
        }

        if (_open.TargetId != buffEvent.TargetId) return;

        _open.Close(buffEvent.Timestamp);
        _open = null;
    }

    [On<CastEvent>(By = Actor.Player, Spells = new[] { nameof(Spells.FluttercallProtect), nameof(Spells.FluttercallEmbrace) })]
    private void OnCast(CastEvent castEvent) => Reassignments++;

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent fightEndEvent) => _open?.Close(fightEndEvent.Timestamp);

    private void Post(BuffEvent buffEvent, int targetId)
    {
        if (!IsBluey(buffEvent.Ability.Id)) return;
        if (_open is { } running && running.TargetId == targetId) return;

        _open?.Close(buffEvent.Timestamp);
        _open = new BlueyPosting(targetId, targetId == PlayerId, buffEvent.Timestamp);
        _postings.Add(_open);
    }

    private static bool IsBluey(int spellId) =>
        spellId == Spells.FluttercallProtectBuff.FSLID || spellId == Spells.FluttercallEmbraceBuff.FSLID;
}

/// <summary>One continuous stretch of Bluey sitting on one unit.</summary>
public sealed class BlueyPosting
{
    internal BlueyPosting(int targetId, bool onSylvie, int start)
    {
        TargetId = targetId;
        OnSylvie = onSylvie;
        Start = start;
    }

    /// <summary>The unit Bluey sat on.</summary>
    public int TargetId { get; }

    /// <summary>Whether that unit is Sylvie herself, which halves the flutterfly bonus but discounts her mana.</summary>
    public bool OnSylvie { get; }

    /// <summary>When Bluey arrived.</summary>
    public int Start { get; }

    /// <summary>When Bluey left, or <c>null</c> while it is still there.</summary>
    public int? End { get; private set; }

    /// <summary>How long Bluey stayed, measured to <paramref name="openEnd"/> while it is still there.</summary>
    public int DurationMs(int openEnd) => Math.Max(0, (End ?? openEnd) - Start);

    internal void Close(int timestamp) => End ??= timestamp;
}
