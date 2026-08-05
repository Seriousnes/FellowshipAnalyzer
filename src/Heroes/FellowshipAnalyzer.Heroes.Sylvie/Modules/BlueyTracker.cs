using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

public sealed partial class BlueyTracker : EventSubscriber
{
    private readonly List<BlueyPosting> _postings = [];

    private BlueyPosting? _open;

    public IReadOnlyList<BlueyPosting> Postings => _postings;

    public int Reassignments { get; private set; }

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

    public int SelfMsBetween(int start, int end) =>
        TimeByHolderBetween(start, end).Where(entry => entry.OnSylvie).Sum(entry => entry.Ms);

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

public sealed class BlueyPosting
{
    internal BlueyPosting(int targetId, bool onSylvie, int start)
    {
        TargetId = targetId;
        OnSylvie = onSylvie;
        Start = start;
    }

    public int TargetId { get; }

    public bool OnSylvie { get; }

    public int Start { get; }

    public int? End { get; private set; }

    public int DurationMs(int openEnd) => Math.Max(0, (End ?? openEnd) - Start);

    internal void Close(int timestamp) => End ??= timestamp;
}
