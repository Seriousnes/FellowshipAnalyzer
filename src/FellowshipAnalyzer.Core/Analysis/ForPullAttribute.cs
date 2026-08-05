namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Boss-state filter for <see cref="ForPullAttribute"/>. A <c>bool?</c> is not a valid attribute
/// argument type, so the three-state choice is expressed as an enum.
/// </summary>
public enum PullBoss
{
    /// <summary>Match boss and non-boss pulls.</summary>
    Either,
    /// <summary>Match boss pulls only.</summary>
    Boss,
    /// <summary>Match non-boss pulls only.</summary>
    NonBoss,
}

/// <summary>
/// Declares the pull shapes an <see cref="Analyzer"/> runs on, and by its presence makes the analyzer
/// pull-lifetime: a fresh instance per matching pull, retained on the pull read surfaces. An analyzer
/// matches a <see cref="Pull"/> when <c>Targets.HasFlag(pull.Targets)</c> and the pull's boss state
/// satisfies <see cref="Boss"/>. Because the filter is compile-time constant, the parser generator
/// emits it as a static bitmask gate in the per-pull resolver.
/// <para>
/// Valid only on a concrete <see cref="Analyzer"/> (diagnostics FA0020 and FA0021): an abstract
/// pull-analyzer base declares the shape and each concrete subclass declares its own filter.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ForPullAttribute(PullKind targets) : Attribute
{
    /// <summary>The pull shapes (<see cref="PullKind.Single"/>, <see cref="PullKind.Multi"/>, or both) this analyzer runs on.</summary>
    public PullKind Targets { get; } = targets;

    /// <summary>The boss state a pull must have to match this analyzer. Defaults to <see cref="PullBoss.Either"/>.</summary>
    public PullBoss Boss { get; set; } = PullBoss.Either;
}
