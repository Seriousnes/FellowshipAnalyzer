using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Measures how Elarion spent <see cref="Spells.CelestialImpetus"/>, the two-stack proc that auto
/// attacks and <see cref="Spells.FocusedShot"/> grant. A <see cref="Spells.CelestialShot"/> cast
/// while the proc is up consumes one stack and applies three Lunarlight Mark stacks off the mark
/// ability's own cooldown, which is the main secondary mark path. That makes the two failure modes
/// symmetrical: a proc that falls off unspent is a mark application thrown away, and a Celestial
/// Shot fired with no proc up spends Focus on the weakest hit in the kit.
/// <para>
/// Stacks are tracked from the player's own buff stream, where the cast is logged before the
/// removal it causes, so the stack count read at a cast is the count going into it. A removal
/// landing within <see cref="ConsumeWindowMs"/> after a Celestial Shot cast is a consumption and
/// every other removal is an expiry. Casts and removals are matched one to one, so a genuine expiry
/// that happens to sit beside an unrelated cast cannot be claimed twice.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class CelestialImpetusAnalyzer : Analyzer
{
    /// <summary>
    /// A buff removal this soon after a Celestial Shot cast counts as consumed rather than expired.
    /// Kept tight because Celestial Shot is cast far more often than the proc is granted, so a wide
    /// window would read expiries beside unrelated casts as spends.
    /// </summary>
    public const int ConsumeWindowMs = 150;

    private readonly List<ShotCast> _celestialShotCasts = [];
    private int _stacks;

    /// <summary>Celestial Impetus stacks granted during the pull, one per apply or stack-gain event.</summary>
    public int ProcsGained { get; private set; }

    /// <summary>Removals matched to a Celestial Shot cast, the spend that reapplies Lunarlight Mark.</summary>
    public int ProcsConsumed { get; private set; }

    /// <summary>
    /// Removals with no Celestial Shot cast beside them, so the proc fell off unspent. Both stacks of
    /// a two-stack proc time out under a single removal, so this counts removal events rather than
    /// stacks lost and the two removal counts do not add up to <see cref="ProcsGained"/>.
    /// </summary>
    public int ProcsExpired { get; private set; }

    /// <summary>Celestial Shot casts made during the pull.</summary>
    public int CelestialShotCasts => _celestialShotCasts.Count;

    /// <summary>Celestial Shot casts started with at least one Celestial Impetus stack banked.</summary>
    public int CelestialShotCastsWithImpetus { get; private set; }

    /// <summary>Celestial Shot casts started with no Celestial Impetus stack to spend.</summary>
    public int CelestialShotCastsWithoutImpetus => CelestialShotCasts - CelestialShotCastsWithImpetus;

    /// <summary>Share of Celestial Shot casts (0-100) that had a Celestial Impetus stack to spend.</summary>
    public double CelestialShotWithImpetusPercentage =>
        CelestialShotCasts == 0 ? 0 : CelestialShotCastsWithImpetus / (double)CelestialShotCasts * 100;

    /// <summary>Pull length in milliseconds.</summary>
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
