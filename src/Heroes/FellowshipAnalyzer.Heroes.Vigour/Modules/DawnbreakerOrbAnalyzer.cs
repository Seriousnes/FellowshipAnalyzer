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
[Dependency<SpellUsable>]
public sealed partial class DawnbreakerOrbAnalyzer : Analyzer
{
    public const int FlightWindowMs = 8_000;

    private readonly List<DawnbreakerOrbCast> _casts = [];
    private readonly ReadyTimeLedger _ready = new();

    public IReadOnlyList<DawnbreakerOrbCast> Casts => _casts;

    public int CastCount => _casts.Count;

    public double? AverageTargetsHit => _casts.Count == 0 ? null : _casts.Average(cast => cast.TargetsHit);

    public int ReadyMs => _ready.ReadyMs(Pull.StartTime, Pull.EndTime);

    public double ReadyShare => Pull.Duration > 0 ? Math.Min(1d, ReadyMs / (double)Pull.Duration) : 0;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e) =>
        _ready.Start(e.StartTime, SpellUsable.CooldownRemaining(Spells.DawnbreakerOrb.FSLID, e.StartTime) <= 0);

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.DawnbreakerOrb))]
    private void OnUsableChanged(UpdateSpellUsableEvent e) => _ready.Observe(e);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.DawnbreakerOrb))]
    private void OnCast(CastEvent e)
    {
        if (e.Fake) return;

        _casts.Add(new DawnbreakerOrbCast(e.Timestamp));
    }

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.DawnbreakerOrb))]
    private void OnHeal(HealEvent e) => Current(e.Timestamp)?.AddAlly(new UnitKey(e.TargetId, e.TargetInstance ?? 0));

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.DawnbreakerOrb))]
    private void OnDamage(DamageEvent e) => Current(e.Timestamp)?.AddEnemy(new UnitKey(e.TargetId, e.TargetInstance ?? 0));

    private DawnbreakerOrbCast? Current(int timestamp) =>
        _casts.Count > 0 && timestamp - _casts[^1].Timestamp <= FlightWindowMs ? _casts[^1] : null;
}
