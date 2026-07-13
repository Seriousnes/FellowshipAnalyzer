using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks time spent at the Highwind Arrow charge cap and casts that occurred while already capped.
/// Highwind Arrow has 3 charges; time at cap means wasted recharge time. Highwind Arrow is the
/// single-target builder (AoE deprioritises it in favour of Multishot and Heartseeker Barrage), so
/// cap discipline is only scored on single-target (boss) pulls.
/// </summary>
[ForPull(PullKind.Single)]
public sealed partial class HighwindArrowCapAnalyzer : Analyzer
{
    private const int MaxCharges = 3;

    private bool _atCap = true;
    private int _capStartTimestamp;
    private int _pullStart;
    private int _pullEnd;

    public int TotalTimeAtCapMs { get; private set; }
    public int CastsWhileCapped { get; private set; }
    public int TotalCasts { get; private set; }
    public int PullDurationMs { get; private set; }

    public double CapPercentage => PullDurationMs > 0 ? (double)TotalTimeAtCapMs / PullDurationMs : 0;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e)
    {
        _pullStart = e.Timestamp;
        _capStartTimestamp = e.Timestamp;
        _atCap = true;
    }

    [On<PullEndEvent>]
    private void OnPullEndEvent(PullEndEvent e)
    {
        _pullEnd = e.Timestamp;
        if (_atCap)
        {
            TotalTimeAtCapMs += e.Timestamp - _capStartTimestamp;
            _atCap = false;
        }
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HighwindArrow))]
    private void OnCast(CastEvent e)
    {
        TotalCasts++;
        if (_atCap)
        {
            CastsWhileCapped++;
        }
    }

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.HighwindArrow))]
    private void OnUpdate(UpdateSpellUsableEvent e)
    {
        var nowCapped = e.ChargesAvailable >= MaxCharges;

        if (nowCapped && !_atCap)
        {
            _capStartTimestamp = e.Timestamp;
            _atCap = true;
        }
        else if (!nowCapped && _atCap)
        {
            TotalTimeAtCapMs += e.Timestamp - _capStartTimestamp;
            _atCap = false;
        }
    }

    public override void OnPullEnd()
    {
        PullDurationMs = Math.Max(0, _pullEnd - _pullStart);
    }
}
