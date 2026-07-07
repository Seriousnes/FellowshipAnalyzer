using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks time spent at the Highwind Arrow charge cap and casts that occurred while already
/// capped. Highwind Arrow has 3 charges; time at cap means wasted recharge time.
/// </summary>
public sealed partial class HighwindArrowCapAnalyzer : Analyzer
{
    private const int MaxCharges = 3;

    private bool _atCap = true;
    private int _capStartTimestamp;
    private int _fightStart;
    private int _fightEnd;
    private int _totalTimeAtCapMs;
    private int _castsWhileCapped;
    private int _totalCasts;

    public int TotalTimeAtCapMs => _totalTimeAtCapMs;
    public int CastsWhileCapped => _castsWhileCapped;
    public int TotalCasts => _totalCasts;

    public double CapPercentage =>
        _fightEnd > _fightStart
            ? (double)_totalTimeAtCapMs / (_fightEnd - _fightStart)
            : 0;

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent e)
    {
        _fightStart = e.Timestamp;
        _capStartTimestamp = e.Timestamp;
        _atCap = true;
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent e)
    {
        _fightEnd = e.Timestamp;
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
}
