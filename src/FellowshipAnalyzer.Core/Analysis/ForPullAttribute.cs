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
/// Declares the pull shapes an <see cref="Analyzer"/> runs on. Required on every Analyzer.
/// An Analyzer matches a <see cref="Pull"/> when
/// <c>(pull.Targets &amp; Targets) != 0</c> and the pull's boss state satisfies <see cref="Boss"/>.
/// Because the filter is compile-time constant, the parser generator emits it as a static bitmask
/// gate in the per-pull resolver.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ForPullAttribute(PullKind targets) : Attribute
{
    /// <summary>The pull shapes (<see cref="PullKind.Single"/>, <see cref="PullKind.Multi"/>, or both) this analyzer runs on.</summary>
    public PullKind Targets { get; } = targets;

    /// <summary>The boss state a pull must have to match this analyzer. Defaults to <see cref="PullBoss.Either"/>.</summary>
    public PullBoss Boss { get; set; } = PullBoss.Either;
}
