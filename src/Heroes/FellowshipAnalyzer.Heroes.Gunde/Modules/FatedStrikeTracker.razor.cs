using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using Items = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public sealed partial class FatedStrikeTracker : EventSubscriber
{
    public const int WindowDurationMs = 6_000;

    public const int SlackMs = 500;

    private int? _windowOpenedAt;

    public int Windows { get; private set; }

    public int FatedStrikeCasts { get; private set; }

    public int PriorityCasts { get; private set; }

    public int FillerCasts { get; private set; }

    public int ClassifiedCasts => PriorityCasts + FillerCasts;

    public double PriorityShare => ClassifiedCasts > 0 ? (double)PriorityCasts / ClassifiedCasts : 0;

    public override StatisticCategory StatisticCategory => StatisticCategory.Items;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Items.FatedStrike))]
    private void OnFatedStrikeCast(CastEvent castEvent) => FatedStrikeCasts++;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Items.GloriousPurpose))]
    private void OnWindowApplied(ApplyBuffEvent buffEvent)
    {
        _windowOpenedAt = buffEvent.Timestamp;
        Windows++;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Items.GloriousPurpose))]
    private void OnWindowRefreshed(RefreshBuffEvent buffEvent)
    {
        if (!IsWindowLive(buffEvent.Timestamp))
            Windows++;

        _windowOpenedAt = buffEvent.Timestamp;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Items.GloriousPurpose))]
    private void OnWindowRemoved(RemoveBuffEvent buffEvent) => _windowOpenedAt = null;

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.GrimCarve),
        nameof(Spells.BloodArc),
        nameof(Spells.HeartSplitter)])]
    private void OnPriorityCast(CastEvent castEvent)
    {
        if (IsWindowLive(castEvent.Timestamp))
            PriorityCasts++;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.DoubleStrike),
        nameof(Spells.ReaverEdge)])]
    private void OnFillerCast(CastEvent castEvent)
    {
        if (IsWindowLive(castEvent.Timestamp))
            FillerCasts++;
    }

    private bool IsWindowLive(int timestamp) =>
        _windowOpenedAt is { } opened && timestamp - opened is >= 0 and <= WindowDurationMs + SlackMs;
}
