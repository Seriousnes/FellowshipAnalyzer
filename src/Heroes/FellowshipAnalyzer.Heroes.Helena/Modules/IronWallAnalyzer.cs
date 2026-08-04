using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// The read surface Iron Wall analysis is published under, keeping it distinct from the other
/// <see cref="MajorDefensiveAnalyzer"/> running on the same pull.
/// </summary>
public interface IIronWallAnalyzer : IAnalyzerSurface;

/// <summary>
/// Measures Iron Wall, which stops Toughness being lowered for its duration rather than reducing a
/// hit, so what it is worth is decided before it goes out: the Toughness it holds, and whether
/// Shields Up charges were spent when it was pressed. Both are recorded per use alongside the damage
/// the window covered.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class IronWallAnalyzer : MajorDefensiveAnalyzer, IIronWallAnalyzer
{
    /// <summary>How long after a cast its buff may apply and still be paired with it.</summary>
    public const int CastPairingGraceMs = 500;

    private readonly List<CastContext> _casts = [];

    /// <inheritdoc/>
    protected override int DefensiveSpellId => Spells.IronWall.FSLID;

    /// <summary>Every Iron Wall window with the state it was pressed in.</summary>
    public IReadOnlyList<IronWallUse> Uses => field ??= BuildUses();

    /// <summary>Iron Walls pressed at or above the 50% Toughness threshold.</summary>
    public int UsesFromHighToughness => Uses.Count(use => use.OpenedAboveHalfToughness);

    /// <summary>Iron Walls pressed with no Shields Up charge left, the window it is meant to buy.</summary>
    public int UsesWithShieldsUpSpent => Uses.Count(use => use.ShieldsUpCharges == 0);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.IronWall))]
    private void OnIronWallCast(CastEvent castEvent) =>
        _casts.Add(new CastContext(
            castEvent.Timestamp,
            ToughnessShare(castEvent.SourceResources),
            SpellUsable.ChargesAvailable(Spells.ShieldsUp.FSLID)));

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.IronWallBuff))]
    private void OnIronWallApplied(ApplyBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.IronWallBuff))]
    private void OnIronWallRefreshed(RefreshBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.IronWallBuff))]
    private void OnIronWallRemoved(RemoveBuffEvent buffEvent) => CloseWindow(buffEvent.Timestamp);

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent) => RecordDamageTaken(damageEvent);

    private IReadOnlyList<IronWallUse> BuildUses()
    {
        var uses = new List<IronWallUse>(Windows.Count);
        foreach (var window in Windows)
        {
            var context = LastCastAtOrBefore(window.Start);
            uses.Add(new IronWallUse(
                window,
                context?.ToughnessShare,
                context?.ToughnessShare is { } share ? ToughnessBands.For(share) : null,
                context?.ShieldsUpCharges));
        }

        return uses;
    }

    private CastContext? LastCastAtOrBefore(int timestamp)
    {
        CastContext? found = null;
        foreach (var context in _casts)
        {
            if (context.Timestamp > timestamp + CastPairingGraceMs) break;
            found = context;
        }

        return found;
    }

    private static double? ToughnessShare(ActorResources? resources)
    {
        if (resources?.Resources is not { Count: > 0 } list) return null;

        foreach (var resource in list)
            if (resource.Type == ResourceTypes.Secondary && resource.Max > 0)
                return resource.Amount / (double)resource.Max;

        return null;
    }

    private sealed record CastContext(int Timestamp, double? ToughnessShare, int ShieldsUpCharges);
}

/// <summary>
/// One Iron Wall window and the state it was pressed in.
/// </summary>
/// <param name="Window">The window itself, carrying the damage it covered.</param>
/// <param name="ToughnessShare">Toughness as a share of maximum when it was pressed, or <c>null</c> when the cast carried no snapshot.</param>
/// <param name="Band">The Toughness band that share falls into.</param>
/// <param name="ShieldsUpCharges">Shields Up charges available when it was pressed, or <c>null</c> when no cast paired with the window.</param>
public sealed record IronWallUse(
    DefensiveWindow Window,
    double? ToughnessShare,
    ToughnessBand? Band,
    int? ShieldsUpCharges)
{
    /// <summary>
    /// Whether Toughness was at or above half maximum, the threshold from which the window holds a
    /// band worth holding rather than a depleted one.
    /// </summary>
    public bool OpenedAboveHalfToughness => ToughnessShare >= ToughnessBands.LowerThreshold(ToughnessBand.Level3);
}
