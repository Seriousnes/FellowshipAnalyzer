using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class CelestialImpetusAnalyzer : Analyzer
{
    public const int ConsumeWindowMs = 150;

    private readonly List<ShotCast> _celestialShotCasts = [];
    private int _stacks;

    public int ProcsGained { get; private set; }

    public int ProcsConsumed { get; private set; }

    public int ProcsExpired { get; private set; }

    public int CelestialShotCasts => _celestialShotCasts.Count;

    public int CelestialShotCastsWithImpetus { get; private set; }

    public int CelestialShotCastsWithoutImpetus => CelestialShotCasts - CelestialShotCastsWithImpetus;

    public double CelestialShotWithImpetusPercentage =>
        CelestialShotCasts == 0 ? 0 : CelestialShotCastsWithImpetus / (double)CelestialShotCasts * 100;

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.CelestialShot))]
    private void OnCelestialShotCast(CastEvent e)
    {
        if (_stacks > 0)
            CelestialShotCastsWithImpetus++;

        _celestialShotCasts.Add(new ShotCast(e.Timestamp));
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.CelestialImpetus))]
    private void OnImpetusApplied(ApplyBuffEvent e)
    {
        _stacks = 1;
        ProcsGained++;
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.CelestialImpetus))]
    private void OnImpetusStackApplied(ApplyBuffStackEvent e)
    {
        _stacks = e.Stack;
        ProcsGained++;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.CelestialImpetus))]
    private void OnImpetusRefreshed(RefreshBuffEvent e) => _stacks = Math.Max(_stacks, 1);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.CelestialImpetus))]
    private void OnImpetusStackRemoved(RemoveBuffStackEvent e)
    {
        _stacks = e.Stack;
        ClassifyRemoval(e.Timestamp);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.CelestialImpetus))]
    private void OnImpetusRemoved(RemoveBuffEvent e)
    {
        _stacks = 0;
        ClassifyRemoval(e.Timestamp);
    }

    private void ClassifyRemoval(int timestamp)
    {
        if (ClaimCast(timestamp))
            ProcsConsumed++;
        else
            ProcsExpired++;
    }

    private bool ClaimCast(int timestamp)
    {
        for (var i = _celestialShotCasts.Count - 1; i >= 0; i--)
        {
            var cast = _celestialShotCasts[i];
            var elapsed = timestamp - cast.Timestamp;
            if (elapsed > ConsumeWindowMs)
                break;

            if (elapsed < 0 || cast.Claimed)
                continue;

            cast.Claimed = true;
            return true;
        }

        return false;
    }

    private sealed class ShotCast(int timestamp)
    {
        public int Timestamp { get; } = timestamp;

        public bool Claimed { get; set; }
    }
}
