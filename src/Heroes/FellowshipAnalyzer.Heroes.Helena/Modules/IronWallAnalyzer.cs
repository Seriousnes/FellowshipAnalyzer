using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public interface IIronWallAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class IronWallAnalyzer : MajorDefensiveAnalyzer, IIronWallAnalyzer
{
    public const int CastPairingGraceMs = 500;

    private readonly List<CastContext> _casts = [];

    protected override int DefensiveSpellId => Spells.IronWall.FSLID;

    public List<IronWallUse> Uses => field ??= BuildUses();

    public int UsesFromHighToughness => Uses.Count(use => use.OpenedAboveHalfToughness);

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

    private List<IronWallUse> BuildUses()
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

public sealed record IronWallUse(
    DefensiveWindow Window,
    double? ToughnessShare,
    ToughnessBand? Band,
    int? ShieldsUpCharges)
{
    public bool OpenedAboveHalfToughness => ToughnessShare >= ToughnessBands.LowerThreshold(ToughnessBand.Level3);
}
