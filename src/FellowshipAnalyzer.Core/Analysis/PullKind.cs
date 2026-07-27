namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The target shape of a <see cref="Pull"/>. A bitfield so an Analyzer can declare the shapes it
/// matches and the pull resolver can test membership with <c>(pull.Targets &amp; declared) != 0</c>.
/// A given pull carries exactly one of these.
/// </summary>
[Flags]
public enum PullKind
{
    /// <summary>A boss pull: exactly one primary enemy.</summary>
    Single = 1,

    /// <summary>A trash pull: multiple enemies.</summary>
    Multi = 2,
}
