using System;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Stamped by the spell-registry generator on each generated spell property, carrying the namespaced
/// <see cref="FSLID.Value"/> as a compile-time constant so the module generator can resolve
/// <c>nameof(Spells.X)</c> to its id across assemblies via metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SpellIdAttribute(int fslid) : Attribute
{
    public int Fslid { get; } = fslid;
}
