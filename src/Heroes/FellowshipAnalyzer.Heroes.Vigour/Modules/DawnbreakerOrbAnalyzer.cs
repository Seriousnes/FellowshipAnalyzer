using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public sealed class DawnbreakerOrbCast(int timestamp)
{
    private readonly HashSet<UnitKey> _allies = [];
    private readonly HashSet<UnitKey> _enemies = [];

    public int Timestamp { get; } = timestamp;

    public int AlliesHit => _allies.Count;

    public int EnemiesHit => _enemies.Count;

    public int TargetsHit => _allies.Count + _enemies.Count;

    internal void AddAlly(UnitKey unit) => _allies.Add(unit);

    internal void AddEnemy(UnitKey unit) => _enemies.Add(unit);
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class DawnbreakerOrbAnalyzer : Analyzer
{
    /// <summary>Milliseconds after a cast within which an Orb heal or damage event belongs to that cast.</summary>
    public const int FlightWindowMs = 8_000;

    private readonly List<DawnbreakerOrbCast> _casts = [];

    public IReadOnlyList<DawnbreakerOrbCast> Casts => _casts;

    public int CastCount => _casts.Count;

    public double? AverageTargetsHit => _casts.Count == 0 ? null : _casts.Average(cast => cast.TargetsHit);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.DawnbreakerOrb))]
    private void OnCast(CastEvent e) => _casts.Add(new DawnbreakerOrbCast(e.Timestamp));

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.DawnbreakerOrb))]
    private void OnHeal(HealEvent e) => Current(e.Timestamp)?.AddAlly(new UnitKey(e.TargetId, e.TargetInstance ?? 0));

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.DawnbreakerOrb))]
    private void OnDamage(DamageEvent e) => Current(e.Timestamp)?.AddEnemy(new UnitKey(e.TargetId, e.TargetInstance ?? 0));

    private DawnbreakerOrbCast? Current(int timestamp) =>
        _casts.Count > 0 && timestamp - _casts[^1].Timestamp <= FlightWindowMs ? _casts[^1] : null;
}
