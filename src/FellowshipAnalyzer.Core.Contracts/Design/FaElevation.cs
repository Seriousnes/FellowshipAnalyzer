namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>The drop shadows. Values are complete CSS <c>box-shadow</c> text.</summary>
public sealed record FaElevation
{
    /// <summary>Card drop shadow.</summary>
    public required string ShadowCard { get; init; }

    /// <summary>Modal and overlay shadow.</summary>
    public required string ShadowLg { get; init; }

    /// <summary>Shadows over the dark grounds, deep enough to read against them.</summary>
    public static FaElevation Original { get; } = Create(FaTint.Medium, FaTint.Strong);

    /// <summary>The dark theme keeps the original shadow weights.</summary>
    public static FaElevation Dark { get; } = Original;

    /// <summary>Cream grounds need a far lighter shadow before it reads as dirt.</summary>
    public static FaElevation Light { get; } = Create(FaTint.Subtle, FaTint.Soft);

    private static FaElevation Create(double cardAlpha, double largeAlpha)
    {
        FaColor black = "#000000";

        return new FaElevation
        {
            ShadowCard = $"0 8px 32px {black.WithAlpha(cardAlpha).ToCss()}",
            ShadowLg = $"0 16px 48px {black.WithAlpha(largeAlpha).ToCss()}",
        };
    }
}
