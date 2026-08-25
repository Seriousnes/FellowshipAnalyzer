namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>How much of its accent colour a <see cref="TipBox"/> carries across its background.</summary>
public enum TipBoxFill
{
    /// <summary>Inset background with the accent confined to the left edge.</summary>
    None,

    /// <summary>Background and border tinted in the accent colour, text unchanged.</summary>
    Tint,

    /// <summary>Background filled with the accent colour, with content set in whichever of black or white contrasts against it.</summary>
    Solid,
}
