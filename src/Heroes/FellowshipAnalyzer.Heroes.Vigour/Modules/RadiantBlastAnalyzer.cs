using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class RadiantBlastAnalyzer : Analyzer
{
    private readonly ReadyTimeLedger _ready = new();

    public int CastCount { get; private set; }

    public int AvatarCasts { get; private set; }

    public int ReadyMs => _ready.ReadyMs(Pull.StartTime, Pull.EndTime);

    public double ReadyShare => Pull.Duration > 0 ? Math.Min(1d, ReadyMs / (double)Pull.Duration) : 0;

    private bool _avatar;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e) =>
        _ready.Start(e.StartTime, SpellUsable.CooldownRemaining(Spells.RadiantBlast.FSLID, e.StartTime) <= 0);

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.RadiantBlast))]
    private void OnUsableChanged(UpdateSpellUsableEvent e) => _ready.Observe(e);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AvatarOfLightBuff))]
    private void OnAvatarApplied(ApplyBuffEvent e)
    {
        _avatar = true;
        _ready.Exclude(e.Timestamp);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AvatarOfLightBuff))]
    private void OnAvatarRemoved(RemoveBuffEvent e)
    {
        _avatar = false;
        _ready.Include(e.Timestamp);
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.RadiantBlast))]
    private void OnCast(CastEvent e)
    {
        if (e.Fake) return;

        CastCount++;
        if (_avatar) AvatarCasts++;
    }
}
