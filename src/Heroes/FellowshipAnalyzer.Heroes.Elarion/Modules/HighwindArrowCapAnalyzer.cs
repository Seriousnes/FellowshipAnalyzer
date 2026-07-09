using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks time spent at the Highwind Arrow charge cap and casts that occurred while already capped.
/// Highwind Arrow has 3 charges; time at cap means wasted recharge time. Highwind Arrow is the
/// single-target builder — AoE deprioritises it in favour of Multishot and Heartseeker Barrage — so
/// cap discipline is only scored on single-target (boss) pulls.
/// </summary>
[ForPull(PullKind.Single)]
public sealed partial class HighwindArrowCapAnalyzer : Analyzer<HighwindArrowCapReport>
{
    private const int MaxCharges = 3;

    private bool _atCap = true;
    private int _capStartTimestamp;
    private int _pullStart;
    private int _pullEnd;
    private int _totalTimeAtCapMs;
    private int _castsWhileCapped;
    private int _totalCasts;

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
            _totalTimeAtCapMs += e.Timestamp - _capStartTimestamp;
            _atCap = false;
        }
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HighwindArrow))]
    private void OnCast(CastEvent e)
    {
        _totalCasts++;
        if (_atCap)
        {
            _castsWhileCapped++;
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
            _totalTimeAtCapMs += e.Timestamp - _capStartTimestamp;
            _atCap = false;
        }
    }

    public override HighwindArrowCapReport OnPullEnd() =>
        new(_totalTimeAtCapMs, _castsWhileCapped, _totalCasts, Math.Max(0, _pullEnd - _pullStart));
}
