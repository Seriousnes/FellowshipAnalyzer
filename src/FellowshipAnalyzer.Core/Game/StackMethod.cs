namespace FellowshipAnalyzer.Core.Common;

/// <summary>
/// How repeat applications of a periodic effect combine on one target.
/// </summary>
public enum StackMethod
{
    /// <summary>
    /// A single instance carrying one damage pool. A fresh application merges the damage still owed
    /// into the incoming amount, so a re-application early in the window forfeits nothing.
    /// </summary>
    Accumulate,

    /// <summary>
    /// A single instance carrying a stack count, each stack adding intensity to every tick. A fresh
    /// application keeps the stacks and extends the window under the pandemic rule.
    /// </summary>
    Stacking,

    /// <summary>
    /// Independent instances coexist on one target, each running its own window, so a fresh
    /// application adds to the number active rather than replacing what is there.
    /// </summary>
    ConcurrentInstances,

    /// <summary>
    /// A single instance with neither stacks nor a pool. A fresh application only refreshes the
    /// window, under the pandemic rule.
    /// </summary>
    Refresh,
}
