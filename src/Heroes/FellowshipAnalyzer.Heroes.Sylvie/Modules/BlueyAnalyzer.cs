using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<BlueyTracker>]
[Dependency<SylvieManaTracker>]
public sealed partial class BlueyAnalyzer : Analyzer
{
    private double _addedOnAlly;
    private double _addedOnSylvie;

    private List<(int TargetId, int Ms, bool OnSylvie)> Result =>
        field ??= BlueyTracker.TimeByHolderBetween(Pull.StartTime, Pull.EndTime);

    public List<(int TargetId, int Ms, bool OnSylvie)> TimeByHolder => Result;

    public int OnAllyMs => Result.Where(entry => !entry.OnSylvie).Sum(entry => entry.Ms);

    public int OnSylvieMs => Result.Where(entry => entry.OnSylvie).Sum(entry => entry.Ms);

    public int UnassignedMs => Math.Max(0, PullDurationMs - OnAllyMs - OnSylvieMs);

    public double OnAllyShare => PullDurationMs > 0 ? OnAllyMs / (double)PullDurationMs : 0;

    public double OnSylvieShare => PullDurationMs > 0 ? OnSylvieMs / (double)PullDurationMs : 0;

    public int Holders => Result.Count;

    public int Reassignments => BlueyTracker.Postings
        .Count(posting => posting.Start > Pull.StartTime && posting.Start <= Pull.EndTime);

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    public long HealingAddedOnAlly => (long)Math.Round(_addedOnAlly);

    public long HealingAddedOnSylvie => (long)Math.Round(_addedOnSylvie);

    public long HealingAdded => (long)Math.Round(_addedOnAlly + _addedOnSylvie);

    public int TicksOnHolder { get; private set; }

    public int ManaSaved => SylvieManaTracker.SavedBetween(Pull.StartTime, Pull.EndTime);

    [On<HealEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.FluttercallHealHot),
        nameof(Spells.FluttercallRestoreLifeHot),
    })]
    private void OnFlutterflyHeal(HealEvent healEvent)
    {
        if (BlueyTracker.PostingAt(healEvent.Timestamp) is not { } posting) return;
        if (posting.TargetId != healEvent.TargetId) return;

        TicksOnHolder++;

        if (posting.OnSylvie)
            _addedOnSylvie += healEvent.Amount * (1 - (1 / SylvieKit.BlueySelfHealScaler));
        else
            _addedOnAlly += healEvent.Amount * (1 - (1 / SylvieKit.BlueyAllyHealScaler));
    }
}
