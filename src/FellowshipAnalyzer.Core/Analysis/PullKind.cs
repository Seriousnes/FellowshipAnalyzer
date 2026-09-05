using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The target shape of a <see cref="PullStartEvent"/>. A bitfield so an Analyzer can declare the shapes it
/// matches and the pull resolver can test membership with <c>declared.HasFlag(pull.Targets)</c>.
/// A given pull has exactly one of these set.
/// </summary>
[Flags]
public enum PullKind
{
    /// <summary>A boss pull: exactly one primary enemy.</summary>
    Single = 1,

    /// <summary>A trash pull: multiple enemies.</summary>
    Multi = 2,
}
