using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

using SylvieTalents = FellowshipAnalyzer.Core.Common.Spells.SylvieTalents;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>Keeps Natural Protector's shields on their own read surface, apart from any other absorb.</summary>
public interface IAbsorbWasteAnalyzer : IAnalyzerSurface;

/// <summary>
/// What Natural Protector's shields absorbed against what expired still on them.
/// <para>
/// Only Natural Protector is measured. Ironleaf Ward reduces damage rather than absorbing it and its
/// removals carry no absorb at all, so it has nothing to waste.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(SylvieTalents.NaturalProtector)]
public sealed partial class AbsorbWasteAnalyzer : AbsorbAnalyzer, IAbsorbWasteAnalyzer
{
    /// <summary>A shield laid on an ally who already carries one is a fresh shield, not an extension.</summary>
    protected override bool ReapplicationOpensNewShield => true;

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnShieldApplied(ApplyBuffEvent buffEvent) => OpenShield(buffEvent);

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnShieldRefreshed(RefreshBuffEvent buffEvent) => OpenShield(buffEvent);

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnShieldRemoved(RemoveBuffEvent buffEvent) => CloseShield(buffEvent);

    [On<AbsorbedEvent>(Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnAbsorbed(AbsorbedEvent absorbedEvent) => RecordAbsorbed(absorbedEvent);
}
