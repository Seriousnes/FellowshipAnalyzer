namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public enum GundePullShape
{
    Boss,

    Aoe,
}

public sealed record SlaughterEvaluation
{
    public required int Timestamp { get; init; }

    public required bool OpenWoundsActive { get; init; }

    public required int TargetsHit { get; init; }

    public required int RendConsumed { get; init; }

    public required long BleedDamage { get; init; }

    public bool WellExecuted { get; init; }
}
