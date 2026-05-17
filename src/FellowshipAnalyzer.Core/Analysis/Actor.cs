namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Identifies the actor an event applies to for declarative <see cref="OnAttribute{TEvent}"/> filters.
/// Flags semantics mirror the legacy <c>SELECTED_PLAYER</c> / <c>SELECTED_PLAYER_PET</c> bitfield.
/// </summary>
[Flags]
public enum Actor
{
    None = 0,
    Self = 1,
    Pet = 2,
    SelfOrPet = Self | Pet,
}
