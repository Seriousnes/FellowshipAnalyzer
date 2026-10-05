using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public sealed partial class RemoveMagicAnalyzer : Analyzer
{
    public const int DispelGraceMs = 50;

    private int? _pending;

    public int Casts { get; private set; }

    public int CastsWithDispel { get; private set; }

    public int Dispels { get; private set; }

    public int CastsWithoutDispel => Casts - CastsWithDispel;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.RemoveMagic))]
    private void OnCast(CastEvent e)
    {
        Casts++;
        _pending = e.Timestamp;
    }

    [On<DispelEvent>(By = Actor.Player, Spell = nameof(Spells.RemoveMagic))]
    private void OnDispel(DispelEvent e)
    {
        Dispels++;
        if (_pending is not { } pending || e.Timestamp > pending + DispelGraceMs) return;

        CastsWithDispel++;
        _pending = null;
    }
}
