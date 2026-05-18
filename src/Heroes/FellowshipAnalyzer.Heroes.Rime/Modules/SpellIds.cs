namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Compile-time integer GUID constants for Rime spells. The values mirror
/// <see cref="Core.Common.Spells.Spell.Guid"/> so they can be used in
/// <see cref="Core.Analysis.OnAttribute{TEvent}"/> arguments,
/// which require compile-time constants.
/// Effect GUIDs follow the <c>1_000_000 + Id</c> encoding used by the combat-log API.
/// </summary>
internal static class SpellIds
{
    public const int BurstingIce = 1031;
    public const int BurstingIceDamage = 1_001_396;
    public const int WintersEmbrace = 1_002_303;
}
