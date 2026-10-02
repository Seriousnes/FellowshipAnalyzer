using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

[Before<SpellUsable>]
[Dependency<SpellUsable>]
public sealed partial class RemoveMagicAnalyzer : Analyzer
{
    public const int DispelGraceMs = 50;

    private int? _pending;

    public int Casts { get; private set; }

    public int CastsWithDispel { get; private set; }

    public int Dispels { get; private set; }

    public int CastsWithoutDispel => Casts - CastsWithDispel;

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        if (_pending is { } pending && e.Timestamp > pending + DispelGraceMs)
        {
            SpellUsable.EndCooldown(Spells.RemoveMagic.FSLID, pending);
            _pending = null;
        }

        if (e.Ability.Id != Spells.RemoveMagic.FSLID) return;

        Casts++;
        _pending = e.Timestamp;
    }

    [On<DispelEvent>(By = Actor.Player, Spell = nameof(Spells.RemoveMagic))]
    private void OnDispel()
    {
        Dispels++;
        if (_pending is null) return;

        CastsWithDispel++;
        _pending = null;
    }
}
