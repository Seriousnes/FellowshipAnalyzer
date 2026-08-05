using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

public sealed partial class SylvieManaTracker : ResourceTracker
{
    private readonly List<ManaSpend> _spends = [];
    private readonly Lazy<BlueyTracker> _bluey;

    private int _lastSnapshot;
    private int _lastAmount;
    private int _lastMax;
    private bool _seeded;

    public SylvieManaTracker(ILogger<ResourceTracker> logger, Lazy<BlueyTracker> bluey) : base(logger)
    {
        _bluey = bluey;
        DisplayNameOverrides[ResourceTypes.Mana] = "Mana";
    }

    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    public IReadOnlyList<ManaSpend> Spends => _spends;

    public int AtFullMs { get; private set; }

    public int MeasuredMs { get; private set; }

    public double AtFullShare => MeasuredMs > 0 ? AtFullMs / (double)MeasuredMs : 0;

    public int ManaSavedByEmbrace { get; private set; }

    public int SpentBetween(int start, int end) =>
        _spends.Where(spend => spend.Timestamp >= start && spend.Timestamp <= end).Sum(spend => spend.Mana);

    public int SavedBetween(int start, int end) =>
        _spends.Where(spend => spend.Timestamp >= start && spend.Timestamp <= end).Sum(spend => spend.Saved);

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

    public int CostOf(CastEvent castEvent)
    {
        var listed = SylvieKit.ManaCost(castEvent.Ability.Id);
        if (listed == 0) return 0;

        var discounted = _bluey.Value.PostingAt(castEvent.Timestamp) is { OnSylvie: true };
        return discounted ? (int)Math.Round(listed * SylvieKit.EmbraceManaCostScaler) : listed;
    }

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

public sealed record ManaSpend(int Timestamp, int SpellId, int Mana, int Saved);
