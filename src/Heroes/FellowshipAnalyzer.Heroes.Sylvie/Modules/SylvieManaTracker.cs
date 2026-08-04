using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Sylvie's mana. The log streams the pool on nearly every event but attaches no cost to a cast, so
/// spending is priced from <see cref="SylvieKit.ManaCosts"/> rather than read off the event, and the
/// discount Bluey grants while parked on Sylvie herself is applied to each cast from the posting that
/// was live when it went out.
/// <para>
/// Regeneration is continuous and the pool is capped, so a stretch spent at full mana is regeneration
/// thrown away. That is measured by time between snapshots, not by counting events, because snapshot
/// density rises with activity and would otherwise read a busy stretch as a long one.
/// </para>
/// </summary>
public sealed partial class SylvieManaTracker : ResourceTracker
{
    private readonly List<ManaSpend> _spends = [];
    private readonly Lazy<BlueyTracker> _bluey;

    private int _lastSnapshot;
    private int _lastAmount;
    private int _lastMax;
    private bool _seeded;

    /// <summary>Creates the tracker and labels the mana pool for the resource UI.</summary>
    public SylvieManaTracker(ILogger<ResourceTracker> logger, Lazy<BlueyTracker> bluey) : base(logger)
    {
        _bluey = bluey;
        DisplayNameOverrides[ResourceTypes.Mana] = "Mana";
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    /// <summary>Every priced cast, in encounter order.</summary>
    public IReadOnlyList<ManaSpend> Spends => _spends;

    /// <summary>Milliseconds spent at a full mana pool, where regeneration had nowhere to go.</summary>
    public int AtFullMs { get; private set; }

    /// <summary>Milliseconds of mana readings taken, the denominator <see cref="AtFullMs"/> is a share of.</summary>
    public int MeasuredMs { get; private set; }

    /// <summary>Share (0-1) of measured time spent at a full mana pool.</summary>
    public double AtFullShare => MeasuredMs > 0 ? AtFullMs / (double)MeasuredMs : 0;

    /// <summary>Mana the Bluey discount took off Sylvie's casts across the whole parse.</summary>
    public int ManaSavedByEmbrace { get; private set; }

    /// <summary>Mana spent on casts between <paramref name="start"/> and <paramref name="end"/>.</summary>
    public int SpentBetween(int start, int end) =>
        _spends.Where(spend => spend.Timestamp >= start && spend.Timestamp <= end).Sum(spend => spend.Mana);

    /// <summary>
    /// Mana the Bluey discount took off casts between <paramref name="start"/> and
    /// <paramref name="end"/>, the difference between each cast's listed cost and what it was charged.
    /// </summary>
    public int SavedBetween(int start, int end) =>
        _spends.Where(spend => spend.Timestamp >= start && spend.Timestamp <= end).Sum(spend => spend.Saved);

    /// <summary>Mana spent per spell between <paramref name="start"/> and <paramref name="end"/>, heaviest first.</summary>
    public IReadOnlyList<(int SpellId, int Mana, int Casts)> SpentBySpellBetween(int start, int end)
    {
        var bySpell = new Dictionary<int, (int Mana, int Casts)>();
        foreach (var spend in _spends)
        {
            if (spend.Timestamp < start || spend.Timestamp > end) continue;

            var current = bySpell.GetValueOrDefault(spend.SpellId);
            bySpell[spend.SpellId] = (current.Mana + spend.Mana, current.Casts + 1);
        }

        return
        [
            .. bySpell
                .Select(entry => (entry.Key, entry.Value.Mana, entry.Value.Casts))
                .OrderByDescending(entry => entry.Mana)
                .ThenBy(entry => entry.Key)
        ];
    }

    /// <summary>
    /// What <paramref name="castEvent"/> cost, after the discount Bluey grants while parked on Sylvie.
    /// Returns zero for an ability the game data prices at nothing.
    /// </summary>
    public int CostOf(CastEvent castEvent)
    {
        var listed = SylvieKit.ManaCost(castEvent.Ability.Id);
        if (listed == 0) return 0;

        var discounted = _bluey.Value.PostingAt(castEvent.Timestamp) is { OnSylvie: true };
        return discounted ? (int)Math.Round(listed * SylvieKit.EmbraceManaCostScaler) : listed;
    }

    /// <inheritdoc/>
    protected override int? GetResourceCost(CastEvent e, ResourceTypes type) =>
        type == ResourceTypes.Mana ? CostOf(e) : null;

    [On<CastEvent>(By = Actor.Player)]
    private void OnSylvieCast(CastEvent castEvent)
    {
        var listed = SylvieKit.ManaCost(castEvent.Ability.Id);
        if (listed == 0) return;

        var cost = CostOf(castEvent);
        var saved = listed - cost;

        ManaSavedByEmbrace += saved;
        _spends.Add(new ManaSpend(castEvent.Timestamp, castEvent.Ability.Id, cost, saved));
    }

    [On<Event>]
    private void OnAnyEvent(Event e)
    {
        var resources = e switch
        {
            IHasSourceEvent source when Owner.ByPlayer(source) => e.SourceResources,
            IHasTargetEvent target when Owner.ToPlayer(target) => e.TargetResources,
            _ => null,
        };

        if (resources?.Resources is not { Count: > 0 } list) return;

        foreach (var resource in list)
        {
            if (resource.Type != ResourceTypes.Mana || resource.Max <= 0) continue;

            if (_seeded)
            {
                var elapsed = e.Timestamp - _lastSnapshot;
                if (elapsed > 0)
                {
                    MeasuredMs += elapsed;
                    if (_lastAmount >= _lastMax) AtFullMs += elapsed;
                }
            }

            _seeded = true;
            _lastSnapshot = e.Timestamp;
            _lastAmount = resource.Amount;
            _lastMax = resource.Max;
            return;
        }
    }
}

/// <summary>One cast's mana cost, priced from the game data rather than read off the event.</summary>
/// <param name="Timestamp">When the cast went out.</param>
/// <param name="SpellId">The ability cast.</param>
/// <param name="Mana">What it cost, after any Bluey discount.</param>
/// <param name="Saved">What the Bluey discount took off the listed cost, zero when Bluey was elsewhere.</param>
public sealed record ManaSpend(int Timestamp, int SpellId, int Mana, int Saved);
