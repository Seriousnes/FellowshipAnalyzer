namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>The edge widths and corner radii. Values are CSS lengths.</summary>
public sealed record FaMetrics
{
    /// <summary>Hairline structural edge.</summary>
    public required string BorderWidth { get; init; }

    /// <summary>Coloured left accent stripe.</summary>
    public required string AccentWidth { get; init; }

    /// <summary>Focus, selection and identity rings.</summary>
    public required string RingWidth { get; init; }

    /// <summary>Tightest corner.</summary>
    public required string RadiusXs { get; init; }

    /// <summary>Panel corner.</summary>
    public required string RadiusSm { get; init; }

    /// <summary>Card corner.</summary>
    public required string RadiusMd { get; init; }

    /// <summary>Large plate corner.</summary>
    public required string RadiusLg { get; init; }

    /// <summary>Feature panel corner.</summary>
    public required string RadiusXl { get; init; }

    /// <summary>Pill and badge corner.</summary>
    public required string RadiusPill { get; init; }

    /// <summary>The one metric system, shared by all three themes.</summary>
    public static FaMetrics Default { get; } = new()
    {
        BorderWidth = "1px",
        AccentWidth = "3px",
        RingWidth = "2px",

        RadiusXs = "3px",
        RadiusSm = "5px",
        RadiusMd = "8px",
        RadiusLg = "12px",
        RadiusXl = "16px",
        RadiusPill = "999px",
    };
}
