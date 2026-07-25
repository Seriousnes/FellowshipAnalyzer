namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// The font stacks and the guide type scale. Values are CSS text, so a stack keeps the quoting a
/// family name with a trailing digit requires.
/// </summary>
public sealed record FaTypography
{
    /// <summary>Body and data.</summary>
    public required string FontBody { get; init; }

    /// <summary>Headings, echoing the wordmark.</summary>
    public required string FontHeading { get; init; }

    /// <summary>Ids, code and data columns.</summary>
    public required string FontMono { get; init; }

    /// <summary>All-caps micro labels.</summary>
    public required string FsLabel { get; init; }

    /// <summary>Metadata rows, inline badges.</summary>
    public required string FsMeta { get; init; }

    /// <summary>Helper and secondary copy.</summary>
    public required string FsBody { get; init; }

    /// <summary>Distribution counts.</summary>
    public required string FsValue { get; init; }

    /// <summary>Panel and section titles.</summary>
    public required string FsTitle { get; init; }

    /// <summary>Headline stat numbers.</summary>
    public required string FsLg { get; init; }

    /// <summary>The one type system, shared by all three themes.</summary>
    public static FaTypography Default { get; } = new()
    {
        FontBody = "\"Source Sans 3\", system-ui, -apple-system, sans-serif",
        FontHeading = "\"Cinzel\", \"Trajan Pro\", Georgia, serif",
        FontMono = "\"Cascadia Code\", \"JetBrains Mono\", ui-monospace, \"SFMono-Regular\", Consolas, monospace",

        FsLabel = "0.66rem",
        FsMeta = "0.78rem",
        FsBody = "0.9rem",
        FsValue = "1rem",
        FsTitle = "1.05rem",
        FsLg = "1.2rem",
    };
}
