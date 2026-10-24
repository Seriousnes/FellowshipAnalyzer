using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>The player's haste over time, read from the <see cref="ChangeHasteEvent"/>s an event stream carries.</summary>
public static class HasteTimeline
{
    /// <summary>
    /// The time-weighted mean haste, as a fraction, across <paramref name="spans"/>. Each span opens on
    /// the haste in force at its start. Reads 0 when the spans cover no time.
    /// </summary>
    /// <param name="events">The analysis event stream, in timestamp order, including fabricated events.</param>
    /// <param name="playerId">The actor id whose haste is read.</param>
    /// <param name="spans">The windows to average over, in dungeon-relative milliseconds.</param>
    public static double Mean(IEnumerable<Event> events, int playerId, IEnumerable<(int Start, int End)> spans)
    {
        var changes = new List<(int Timestamp, double Haste)>();
        foreach (var e in events)
            if (e is ChangeHasteEvent { NewHaste: { } newHaste } change && change.SourceId == playerId)
                changes.Add((change.Timestamp, newHaste));

        double weighted = 0;
        long covered = 0;

        foreach (var (start, end) in spans)
        {
            if (end <= start) continue;

            var haste = 0.0;
            var at = start;
            foreach (var (timestamp, value) in changes)
            {
                if (timestamp <= start)
                {
                    haste = value;
                    continue;
                }

                if (timestamp >= end) break;

                weighted += haste * (timestamp - at);
                at = timestamp;
                haste = value;
            }

            weighted += haste * (end - at);
            covered += end - start;
        }

        return covered > 0 ? weighted / covered : 0;
    }
}
