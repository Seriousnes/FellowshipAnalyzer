namespace FellowshipAnalyzer.Core.Insights.Aeona;

/// <summary>Name of each Aeona guide with insights, as the request route and the user message name it.</summary>
public static class AeonaGuides
{
    /// <summary>Hero name, as the request route names it.</summary>
    public const string Hero = "aeona";

    /// <summary>Entropy's Claim guide.</summary>
    public const string EntropyClaim = "entropyClaim";

    /// <summary>Oblivion guide.</summary>
    public const string Oblivion = "oblivion";

    /// <summary>Amend Fate and Restore Continuity guide.</summary>
    public const string StaggerCleanse = "staggerCleanse";

    /// <summary>Continuum Shift guide.</summary>
    public const string ContinuumShift = "continuumShift";

    /// <summary>Kind Rewind guide.</summary>
    public const string KindRewind = "kindRewind";
}

/// <summary>The fixed strings Aeona's insight messages use beyond <see cref="DigestValues"/>.</summary>
public static class AeonaDigestValues
{
    /// <summary>An Entropy's Claim cast before the window.</summary>
    public const string Early = "early";

    /// <summary>An Entropy's Claim cast inside the window.</summary>
    public const string InWindow = "in window";

    /// <summary>An Entropy's Claim cast after the window.</summary>
    public const string Late = "late";

    /// <summary>An Entropy's Claim cast with no Entropy's Claim active.</summary>
    public const string NoneActive = "none active";

    /// <summary>A Continuum Shift ended unspent.</summary>
    public const string Nothing = "nothing";

    /// <summary>A Continuum Shift still open when the pull ended.</summary>
    public const string OpenAtPullEnd = "open at pull end";

    /// <summary>Oblivion cast while a cleanse was due on many casts.</summary>
    public const string Often = "often";

    /// <summary>Every string on this class.</summary>
    public static IReadOnlyList<string> All { get; } = [Early, InWindow, Late, NoneActive, Nothing, OpenAtPullEnd, Often];
}
