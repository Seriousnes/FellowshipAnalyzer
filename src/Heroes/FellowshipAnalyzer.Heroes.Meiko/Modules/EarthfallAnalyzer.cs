using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Meiko;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

public sealed record RisingEarthCast(int Timestamp, int EarthfallHeld);

public sealed record EarthfistBarrageCast(int Timestamp, int EarthfallHeld)
{
    public bool Empowered => EarthfallHeld > 0;
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class EarthfallAnalyzer : Analyzer
{
    private readonly List<RisingEarthCast> _risingEarthCasts = [];
    private readonly List<EarthfistBarrageCast> _barrageCasts = [];

    private int _earthfall;
    private int _gained;

    public IReadOnlyList<RisingEarthCast> RisingEarthCasts => _risingEarthCasts;

    public IReadOnlyList<EarthfistBarrageCast> BarrageCasts => _barrageCasts;

    public int EarthfallGranted => _gained + EarthfallOverwritten;

    public int EarthfallOverwritten => _risingEarthCasts.Sum(cast => cast.EarthfallHeld);

    public int RisingEarthOverwrites => _risingEarthCasts.Count(cast => cast.EarthfallHeld > 0);

    public int EmpoweredBarrages => _barrageCasts.Count(cast => cast.Empowered);

    public int UnempoweredBarrages => _barrageCasts.Count(cast => !cast.Empowered);

    public double? OverwriteShare => _risingEarthCasts.Count == 0 ? null : (double)RisingEarthOverwrites / _risingEarthCasts.Count;

    public double? EmpoweredShare => _barrageCasts.Count == 0 ? null : (double)EmpoweredBarrages / _barrageCasts.Count;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent pullStart) =>
        _earthfall = Owner.SelectedCombatant.HasBuff(Spells.Earthfall, pullStart.Timestamp)
            ? Math.Max(1, Owner.SelectedCombatant.GetBuffStacks(Spells.Earthfall, pullStart.Timestamp))
            : 0;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.RisingEarth))]
    private void OnRisingEarth(CastEvent castEvent) =>
        _risingEarthCasts.Add(new RisingEarthCast(castEvent.Timestamp, _earthfall));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.EarthfistBarrage))]
    private void OnEarthfistBarrage(CastEvent castEvent) =>
        _barrageCasts.Add(new EarthfistBarrageCast(castEvent.Timestamp, _earthfall));

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Earthfall))]
    private void OnApplied() => SetEarthfall(1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.Earthfall))]
    private void OnStackGained(ApplyBuffStackEvent buffEvent) => SetEarthfall(buffEvent.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.Earthfall))]
    private void OnStackLost(RemoveBuffStackEvent buffEvent) => _earthfall = buffEvent.Stack;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Earthfall))]
    private void OnRemoved() => _earthfall = 0;

    private void SetEarthfall(int stacks)
    {
        _gained += Math.Max(0, stacks - _earthfall);
        _earthfall = stacks;
    }
}
