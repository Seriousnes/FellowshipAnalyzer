namespace FellowshipAnalyzer.Core.Events;

/// <summary>Reports the portion of an incoming heal that a damage absorb shield consumed instead of restoring health.</summary>
[Fabricated]
public class HealAbsorbed : HealEvent
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
