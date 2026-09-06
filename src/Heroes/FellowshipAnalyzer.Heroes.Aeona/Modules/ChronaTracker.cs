using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.Utility;

using Microsoft.Extensions.Logging;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;
using Talents = FellowshipAnalyzer.Core.Common.Spells.Aeona.Talents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Tracks the two resources Aeona's kit moves: Chrona on <see cref="ResourceTypes.Primary"/> and
/// mana on <see cref="ResourceTypes.Mana"/>. Registered dungeon-lifetime, so it accumulates across
/// the whole report and per-pull analyzers read a slice of it through the windowed accessors.
/// </summary>
/// <remarks>
/// <para>
/// Two views of the same resources live on this type. The inherited <see cref="ResourceTracker.GetGenerated"/>,
/// <see cref="ResourceTracker.GetWasted"/> and <see cref="ResourceTracker.GetSpent"/> are the base tracker's
/// raw-event view: gains from <see cref="ResourceChangeEvent"/> and spends from a cast's declared cost.
/// The <c>Between</c> family, <see cref="AmountAt"/> and <see cref="MaxOf"/> are a reconstructed view built
/// from the resource blocks on events.
/// </para>
/// <para>
/// The reconstruction reads <see cref="Event.SourceResources"/> and <see cref="Event.TargetResources"/> on
/// every event touching the selected player, and turns each change in a tracked resource's amount into a
/// <see cref="ResourceEvent"/>. Those blocks are sparse, so the reconstruction never assumes an entry per
/// event; it only compares consecutive ones.
/// </para>
/// </remarks>
public sealed partial class ChronaTracker : ResourceTracker
{
    /// <summary>Chrona's cap when nothing has reported a maximum for <see cref="ResourceTypes.Primary"/>.</summary>
    private const int DefaultChronaCap = 100;

    /// <summary>
    /// How long after a generating damage or cast event the gain it produced may be reported. Fellowship
    /// puts the raised amount on the event after the one that generated it, so the gap is the distance to
    /// the next event rather than a game rule.
    /// </summary>
    public const int GenerationAttributionWindowMs = 1_000;

    private readonly Dictionary<ResourceTypes, ResourceLedger> _ledgers = [];
    private readonly List<GeneratorEvent> _generators = [];
    private readonly List<AuraWindow> _continuumShiftWindows = [];

    private int? _continuumShiftOpenedAt;

    private int _lastSpenderId;

    /// <summary>Creates the tracker and labels <see cref="ResourceTypes.Primary"/> as Chrona for resource UI.</summary>
    public ChronaTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        DisplayNameOverrides[ResourceTypes.Primary] = "Chrona";
    }

    /// <summary>
    /// The usable amount of <paramref name="type"/> generated across the whole report, excluding the portion
    /// lost to the cap. The reconstructed counterpart to <see cref="ResourceTracker.GetGenerated"/>.
    /// </summary>
    public int TotalGenerated(ResourceTypes type) =>
        _ledgers.TryGetValue(type, out var ledger) ? ledger.Generated : 0;

    /// <summary>
    /// The amount of <paramref name="type"/> generated but lost to the cap across the whole report. The
    /// reconstructed counterpart to <see cref="ResourceTracker.GetWasted"/>.
    /// </summary>
    public int TotalWasted(ResourceTypes type) =>
        _ledgers.TryGetValue(type, out var ledger) ? ledger.Wasted : 0;

    /// <summary>
    /// The amount of <paramref name="type"/> spent across the whole report. The reconstructed counterpart to
    /// <see cref="ResourceTracker.GetSpent"/>, net of generation arriving in the same interval.
    /// </summary>
    public int TotalSpent(ResourceTypes type) =>
        _ledgers.TryGetValue(type, out var ledger) ? ledger.Spent : 0;

    /// <summary>
    /// The total usable amount of <paramref name="type"/> generated between <paramref name="start"/> and
    /// <paramref name="end"/> inclusive. Excludes the portion lost to the cap, which
    /// <see cref="WastedBetween"/> reports.
    /// </summary>
    public int GeneratedBetween(ResourceTypes type, int start, int end)
    {
        var total = 0;
        foreach (var resourceEvent in EventsBetween(type, start, end))
            if (resourceEvent.Kind == ResourceEventKind.Gain)
                total += resourceEvent.Amount;

        return total;
    }

    /// <summary>
    /// The total amount of <paramref name="type"/> generated but lost to the cap between
    /// <paramref name="start"/> and <paramref name="end"/> inclusive. A gain includes the waste a
    /// <see cref="ResourceChangeEvent"/> declared, plus any reconstructed excess above
    /// <see cref="MaxOf"/> the gain would have crossed.
    /// </summary>
    public int WastedBetween(ResourceTypes type, int start, int end)
    {
        var total = 0;
        foreach (var resourceEvent in EventsBetween(type, start, end))
            total += resourceEvent.Wasted;

        return total;
    }

    /// <summary>
    /// The total amount of <paramref name="type"/> spent between <paramref name="start"/> and
    /// <paramref name="end"/> inclusive. Reconstructed from a falling pool, so a spend is net of any
    /// generation that arrived in the same interval and is a lower bound on the ability's true cost.
    /// </summary>
    public int SpentBetween(ResourceTypes type, int start, int end)
    {
        var total = 0;
        foreach (var resourceEvent in EventsBetween(type, start, end))
            if (resourceEvent.Kind == ResourceEventKind.Spend)
                total += resourceEvent.Amount;

        return total;
    }

    /// <summary>
    /// Every reconstructed change to <paramref name="type"/> between <paramref name="start"/> and
    /// <paramref name="end"/> inclusive, in chronological order. <see cref="ResourceEvent.Id"/> is the FSLID
    /// of the ability the change is attributed to: for a gain, the ability of the event the resource block
    /// sat on, or <c>0</c> when that event is a cast, whose block precedes its own effect; for a spend,
    /// the player's most recent cast. <see cref="CombatLogParser"/> drops every cast FellowshipLogs marks
    /// <see cref="CastEvent.Fake"/> before dispatch, so the attributed cast is always one that completed.
    /// </summary>
    public IReadOnlyList<ResourceEvent> EventsBetween(ResourceTypes type, int start, int end)
    {
        if (!_ledgers.TryGetValue(type, out var ledger)) return [];

        var window = new List<ResourceEvent>();
        foreach (var resourceEvent in ledger.Events)
            if (resourceEvent.Timestamp >= start && resourceEvent.Timestamp <= end)
                window.Add(resourceEvent);

        return window;
    }

    /// <summary>
    /// Generation of <paramref name="type"/> between <paramref name="start"/> and <paramref name="end"/>
    /// inclusive, split by the ability each gain is attributed to and ordered by usable amount. Unattributed
    /// gains, which are those on a cast's own resource block, are grouped under ability id <c>0</c>.
    /// </summary>
    public IReadOnlyList<AbilityResourceGain> GeneratedByAbilityBetween(ResourceTypes type, int start, int end)
    {
        var byAbility = new Dictionary<int, (int Generated, int Wasted)>();
        foreach (var resourceEvent in EventsBetween(type, start, end))
        {
            if (resourceEvent.Kind != ResourceEventKind.Gain) continue;

            var running = byAbility.GetValueOrDefault(resourceEvent.Id);
            byAbility[resourceEvent.Id] =
                (running.Generated + resourceEvent.Amount, running.Wasted + resourceEvent.Wasted);
        }

        return
        [
            .. byAbility
                .Select(entry => new AbilityResourceGain(entry.Key, entry.Value.Generated, entry.Value.Wasted))
                .OrderByDescending(gain => gain.Generated)
                .ThenBy(gain => gain.AbilityId)
        ];
    }

    /// <summary>
    /// Every gain of <paramref name="type"/> between <paramref name="start"/> and <paramref name="end"/>
    /// inclusive, in chronological order, with the amount the game data says the generating ability
    /// produces set against the rise the pool took.
    /// </summary>
    /// <remarks>
    /// The pool is the ground truth: a rise it actually took is what <see cref="ResourceGain.Usable"/>
    /// reports. A rise is already clipped by the cap, so it cannot show overcap. The stated amount fills
    /// that gap: where it exceeds the rise, the difference is <see cref="ResourceGain.Overcap"/>. This is
    /// also the only rule that survives the game rolling the critical amount independently of the damage
    /// event's own critical flag.
    /// </remarks>
    /// <param name="type">The resource to read.</param>
    /// <param name="start">The first instant to include.</param>
    /// <param name="end">The last instant to include.</param>
    public IReadOnlyList<ResourceGain> GainsBetween(ResourceTypes type, int start, int end)
    {
        var gains = new List<ResourceGain>();

        foreach (var resourceEvent in EventsBetween(type, start, end))
        {
            if (resourceEvent.Kind != ResourceEventKind.Gain) continue;

            var before = resourceEvent.CurrentAfter - resourceEvent.Amount;
            var generator = GeneratorAt(resourceEvent.Timestamp, resourceEvent.Id);
            var stated = StatedAmount(type, generator, before);

            gains.Add(new ResourceGain(
                resourceEvent.Timestamp,
                generator?.Timestamp ?? resourceEvent.Timestamp,
                generator?.AbilityId ?? resourceEvent.Id,
                generator?.Target,
                before,
                Math.Max(stated, resourceEvent.Amount),
                resourceEvent.Amount,
                Math.Max(0, stated - resourceEvent.Amount),
                SynchronicityShare(type, generator, before, resourceEvent.Amount)));
        }

        return gains;
    }

    /// <summary>
    /// The amount of <paramref name="type"/> lost to the cap between <paramref name="start"/> and
    /// <paramref name="end"/> inclusive.
    /// </summary>
    /// <param name="type">The resource to read.</param>
    /// <param name="start">The first instant to include.</param>
    /// <param name="end">The last instant to include.</param>
    public int OvercapBetween(ResourceTypes type, int start, int end) =>
        GainsBetween(type, start, end).Sum(gain => gain.Overcap);

    /// <summary>
    /// The usable amount of <paramref name="type"/> that <paramref name="abilityId"/> generated between
    /// <paramref name="start"/> and <paramref name="end"/> inclusive.
    /// </summary>
    /// <param name="type">The resource to read.</param>
    /// <param name="abilityId">The generating ability's FSLID.</param>
    /// <param name="start">The first instant to include.</param>
    /// <param name="end">The last instant to include.</param>
    public int GeneratedByAbilityBetween(ResourceTypes type, int abilityId, int start, int end) =>
        GainsBetween(type, start, end).Where(gain => gain.AbilityId == abilityId).Sum(gain => gain.Usable);

    /// <summary>
    /// The amount of <paramref name="type"/> that <paramref name="abilityId"/> lost to the cap between
    /// <paramref name="start"/> and <paramref name="end"/> inclusive.
    /// </summary>
    /// <param name="type">The resource to read.</param>
    /// <param name="abilityId">The generating ability's FSLID.</param>
    /// <param name="start">The first instant to include.</param>
    /// <param name="end">The last instant to include.</param>
    public int OvercapByAbilityBetween(ResourceTypes type, int abilityId, int start, int end) =>
        GainsBetween(type, start, end).Where(gain => gain.AbilityId == abilityId).Sum(gain => gain.Overcap);

    /// <summary>
    /// The amount of <paramref name="type"/> one hit of <paramref name="abilityId"/> at
    /// <paramref name="timestamp"/> lost to the cap, which is the whole amount the game data states for
    /// that ability while the player was at the maximum, and <c>0</c> anywhere below it.
    /// </summary>
    /// <param name="type">The resource to read.</param>
    /// <param name="abilityId">The generating ability's FSLID.</param>
    /// <param name="timestamp">The instant of the hit.</param>
    public int GenerationLostAtMaximum(ResourceTypes type, int abilityId, int timestamp)
    {
        if (!_ledgers.TryGetValue(type, out var ledger) || ledger.Max <= 0) return 0;

        var amount = AmountAt(type, timestamp);
        if (amount < ledger.Max) return 0;

        return StatedAmount(
            type,
            new GeneratorEvent(timestamp, ResolveAbility(abilityId), Target: null, PerHit: true),
            amount);
    }

    /// <summary>
    /// The amount of <paramref name="type"/> most recently reported at or before
    /// <paramref name="timestamp"/>, or <see langword="null"/> when nothing precedes it.
    /// <see cref="AmountAt"/> reports the same figure with <c>0</c> in place of the null.
    /// </summary>
    /// <param name="type">The resource to read.</param>
    /// <param name="timestamp">The instant to read back from.</param>
    public int? SnapshotAt(ResourceTypes type, int timestamp)
    {
        if (!_ledgers.TryGetValue(type, out var ledger) || ledger.Samples.Count == 0) return null;
        if (ledger.Samples[0].Timestamp > timestamp) return null;

        return AmountAt(type, timestamp);
    }

    /// <summary>
    /// The amount of <paramref name="type"/> the player held at or before <paramref name="timestamp"/>,
    /// or <c>0</c> when nothing precedes it.
    /// </summary>
    public int AmountAt(ResourceTypes type, int timestamp)
    {
        if (!_ledgers.TryGetValue(type, out var ledger)) return 0;

        var samples = ledger.Samples;
        var low = 0;
        var high = samples.Count - 1;
        var latest = -1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (samples[middle].Timestamp <= timestamp)
            {
                latest = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return latest < 0 ? 0 : samples[latest].Amount;
    }

    /// <summary>
    /// The cap on <paramref name="type"/>: the highest maximum reported, falling back to
    /// <see cref="DefaultChronaCap"/> for Chrona and to <c>0</c> for a resource whose maximum never appeared.
    /// </summary>
    public int MaxOf(ResourceTypes type)
    {
        if (_ledgers.TryGetValue(type, out var ledger) && ledger.Max > 0) return ledger.Max;

        return type == ResourceTypes.Primary ? DefaultChronaCap : 0;
    }

    /// <inheritdoc/>
    protected override int? GetResourceCost(CastEvent e, ResourceTypes type)
    {
        if (!IsTracked(type)) return base.GetResourceCost(e, type);

        return SpellRegistry.MaybeGet(e.Ability.FSLID)?.Cost(type);
    }

    [On<Event>]
    private void OnResourceSnapshot(Event e)
    {
        if (e is ResourceChangeEvent) return;

        var resources = e switch
        {
            IHasSourceEvent source when Owner.ByPlayer(source) => e.SourceResources,
            IHasTargetEvent target when Owner.ToPlayer(target) => e.TargetResources,
            _ => null,
        };

        var isCast = e is BaseCastEvent;

        if (resources?.Resources is { Count: > 0 } block)
        {
            var eventAbilityId = isCast ? 0 : (e as IAbilityEvent)?.Ability.Id ?? 0;

            foreach (var resource in block)
                if (IsTracked(resource.Type))
                    Observe(resource, eventAbilityId, e.Timestamp);
        }

        if (e is CastEvent cast && Owner.ByPlayer(cast))
            _lastSpenderId = cast.Ability.Id;
    }

    [On<ResourceChangeEvent>(By = Actor.Player)]
    private void OnPlayerResourceChange(ResourceChangeEvent e)
    {
        if (!IsTracked(e.ResourceChangeType)) return;

        var declaredWaste = (int)e.Waste;
        var gained = (int)e.ResourceChange - declaredWaste;
        if (gained <= 0 && declaredWaste <= 0) return;

        var ledger = GetOrCreateLedger(e.ResourceChangeType);
        RecordGain(ledger, e.ResourceChangeType, e.Ability.Id, gained, declaredWaste, observedAmount: null, e.Timestamp);
        ledger.Samples.Add(new Sample(e.Timestamp, ledger.Amount));
    }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnGeneratingDamage(DamageEvent e) =>
        _generators.Add(new GeneratorEvent(
            e.Timestamp,
            ResolveAbility(e.Ability?.Id ?? 0),
            AuraWindowLedger.KeyOf(e),
            PerHit: true));

    [On<CastEvent>(By = Actor.Player)]
    private void OnGeneratingCast(CastEvent e) =>
        _generators.Add(new GeneratorEvent(e.Timestamp, ResolveAbility(e.Ability.Id), Target: null, PerHit: false));

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnContinuumShiftApplied(ApplyBuffEvent e) => _continuumShiftOpenedAt ??= e.Timestamp;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ContinuumShift))]
    private void OnContinuumShiftRemoved(RemoveBuffEvent e)
    {
        if (_continuumShiftOpenedAt is not { } start) return;

        _continuumShiftWindows.Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
        _continuumShiftOpenedAt = null;
    }

    /// <summary>
    /// The FSLID the game data states the generation amount for. A damage event names the effect rather
    /// than the ability that owns it, so the spellbook resolves it back through its additional spells.
    /// </summary>
    private int ResolveAbility(int abilityId) =>
        Spellbook?.GetAbility(abilityId)?.PrimarySpell.FSLID ?? abilityId;

    private Abilities? Spellbook => field ??= Owner.GetModule<Abilities>();

    /// <summary>
    /// The generating event a gain at <paramref name="timestamp"/> is attributed to: the player's most
    /// recent damage or cast at or before it, preferring one whose own ability matches
    /// <paramref name="eventAbilityId"/>.
    /// </summary>
    private GeneratorEvent? GeneratorAt(int timestamp, int eventAbilityId)
    {
        GeneratorEvent? nearest = null;

        for (var i = _generators.Count - 1; i >= 0; i--)
        {
            var generator = _generators[i];
            if (generator.Timestamp > timestamp) continue;
            if (timestamp - generator.Timestamp > GenerationAttributionWindowMs) break;

            if (generator.AbilityId == ResolveAbility(eventAbilityId)) return generator;

            nearest ??= generator;
        }

        return nearest;
    }

    /// <summary>
    /// The amount the game data says <paramref name="generator"/> produces, with every modifier the build
    /// has applied. Zero when the registry states no generation for it, which leaves the rise on its own.
    /// </summary>
    private int StatedAmount(ResourceTypes type, GeneratorEvent? generator, int before)
    {
        if (generator is not { } source) return 0;
        if (SpellRegistry.MaybeGet(source.AbilityId)?.ResourceGeneration is not { } generation) return 0;
        if (generation.Resource != type) return 0;

        var amount = generation.Measure == GenerationMeasure.FractionOfMaximum
            ? generation.Amount * MaxOf(type)
            : generation.Amount;

        return (int)Math.Round(amount * (1 + SynchronicityIncrease(type, before) + ContinuumShiftIncrease(type, source)));
    }

    /// <summary>
    /// The share of a gain that Synchronicity added, which the talent grants while the player holds less
    /// than half the maximum.
    /// </summary>
    private int SynchronicityShare(ResourceTypes type, GeneratorEvent? generator, int before, int usable)
    {
        var increase = SynchronicityIncrease(type, before);
        if (increase <= 0 || generator is null) return 0;

        return (int)Math.Round(usable * increase / (1 + increase));
    }

    private double SynchronicityIncrease(ResourceTypes type, int before)
    {
        if (type != ResourceTypes.Primary) return 0;
        if (!Owner.SelectedCombatant.HasTalent(AeonaTalents.Synchronicity)) return 0;
        if (before >= MaxOf(type) / 2d) return 0;

        return Talents.Synchronicity.ResourceGeneration?.Amount ?? 0;
    }

    private double ContinuumShiftIncrease(ResourceTypes type, GeneratorEvent generator)
    {
        if (type != ResourceTypes.Primary) return 0;
        if (generator.AbilityId != Spells.TimeShard.FSLID) return 0;
        if (!Owner.SelectedCombatant.HasTalent(AeonaTalents.ContinuumShift)) return 0;
        if (!ContinuumShiftCovers(generator.Timestamp)) return 0;

        return Talents.ContinuumShift.ResourceGeneration?.Amount ?? 0;
    }

    private bool ContinuumShiftCovers(int timestamp)
    {
        if (_continuumShiftOpenedAt is { } open && timestamp >= open) return true;

        foreach (var window in _continuumShiftWindows)
        {
            if (timestamp >= window.Start && timestamp <= window.End) return true;
        }

        return false;
    }

    private void Observe(ClassResource resource, int eventAbilityId, int timestamp)
    {
        var ledger = GetOrCreateLedger(resource.Type);

        if (resource.Max > 0)
            ledger.Max = resource.Max;

        if (ledger.Seeded)
        {
            var delta = resource.Amount - ledger.Amount;
            if (delta > 0)
                RecordGain(ledger, resource.Type, eventAbilityId, delta, declaredWaste: 0, resource.Amount, timestamp);
            else if (delta < 0)
                RecordSpend(ledger, resource.Type, -delta, resource.Amount, timestamp);
        }
        else
        {
            ledger.Seeded = true;
            ledger.Amount = resource.Amount;
        }

        ledger.Samples.Add(new Sample(timestamp, ledger.Amount));
    }

    private static void RecordGain(
        ResourceLedger ledger,
        ResourceTypes type,
        int abilityId,
        int gained,
        int declaredWaste,
        int? observedAmount,
        int timestamp)
    {
        var cap = ledger.Max;
        var after = ledger.Amount + gained;
        var overcap = cap > 0 && after > cap ? after - cap : 0;
        var usable = gained - overcap;
        var wasted = declaredWaste + overcap;

        ledger.Generated += usable;
        ledger.Wasted += wasted;
        ledger.Amount = observedAmount ?? (cap > 0 ? Math.Min(after, cap) : after);
        ledger.Events.Add(new ResourceEvent(
            timestamp, abilityId, type, ResourceEventKind.Gain, usable, wasted, ledger.Amount, cap));
    }

    private void RecordSpend(ResourceLedger ledger, ResourceTypes type, int spent, int observedAmount, int timestamp)
    {
        ledger.Spent += spent;
        ledger.Amount = observedAmount;
        ledger.Events.Add(new ResourceEvent(
            timestamp, _lastSpenderId, type, ResourceEventKind.Spend, spent, Wasted: 0, ledger.Amount, ledger.Max));
    }

    private ResourceLedger GetOrCreateLedger(ResourceTypes type)
    {
        if (!_ledgers.TryGetValue(type, out var ledger))
        {
            ledger = new ResourceLedger();
            _ledgers[type] = ledger;
        }

        return ledger;
    }

    private static bool IsTracked(ResourceTypes type) =>
        type is ResourceTypes.Primary or ResourceTypes.Mana;

    private readonly record struct Sample(int Timestamp, int Amount);

    /// <summary>One player damage or cast that the game data says generates a resource.</summary>
    private readonly record struct GeneratorEvent(int Timestamp, int AbilityId, UnitKey? Target, bool PerHit);

    private sealed class ResourceLedger
    {
        public bool Seeded { get; set; }
        public int Amount { get; set; }
        public int Max { get; set; }
        public int Generated { get; set; }
        public int Wasted { get; set; }
        public int Spent { get; set; }
        public List<ResourceEvent> Events { get; } = [];
        public List<Sample> Samples { get; } = [];
    }
}

/// <summary>
/// One gain of a resource, with the rise the pool took set against the amount the game data says the
/// generating ability produces.
/// </summary>
/// <param name="Timestamp">When the gain arrived.</param>
/// <param name="GeneratorTimestamp">The timestamp of the damage or cast the gain is attributed to.</param>
/// <param name="AbilityId">The FSLID of the ability the gain is attributed to.</param>
/// <param name="Target">The enemy whose hit produced the gain, or null for a gain a cast produced.</param>
/// <param name="Before">The amount the player held before the gain.</param>
/// <param name="Gain">What the gain would have been with room for all of it.</param>
/// <param name="Usable">The rise the player's pool actually took.</param>
/// <param name="Overcap">The share of <paramref name="Gain"/> the cap discarded.</param>
/// <param name="SynchronicityChrona">The share of <paramref name="Usable"/> that Synchronicity added.</param>
public sealed record ResourceGain(
    int Timestamp,
    int GeneratorTimestamp,
    int AbilityId,
    UnitKey? Target,
    int Before,
    int Gain,
    int Usable,
    int Overcap,
    int SynchronicityChrona);

/// <summary>One ability's share of a resource's reconstructed generation over a window.</summary>
/// <param name="AbilityId">The FSLID of the ability the gains are attributed to, or <c>0</c> when unattributed.</param>
/// <param name="Generated">The usable amount generated.</param>
/// <param name="Wasted">The amount generated but lost to the cap.</param>
public sealed record AbilityResourceGain(int AbilityId, int Generated, int Wasted);
