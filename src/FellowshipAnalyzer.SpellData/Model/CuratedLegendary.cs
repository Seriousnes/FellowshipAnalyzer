namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>
/// One legendary item selected into a hero scope: the member is named from the power the item grants,
/// because that is the name players use for the build.
/// </summary>
public record CuratedLegendary(
    string Scope,
    string Member,
    int ItemId,
    string ItemName,
    int PowerId,
    string PowerName,
    string Slot,
    string Icon);
