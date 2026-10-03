using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class RadiantBlastAnalyzer : Analyzer
{
    private readonly ReadyTimeLedger _ready = new();
    private bool _avatar;

    public int CastCount { get; private set; }

    public int AvatarCasts { get; private set; }

    public int ReadyMs => _ready.ReadyMs(Pull.StartTime, Pull.EndTime);

    public int MeasuredMs => Math.Max(0, Pull.Duration - _ready.ExcludedMs(Pull.StartTime, Pull.EndTime));

    public double ReadyShare => MeasuredMs > 0 ? Math.Min(1d, ReadyMs / (double)MeasuredMs) : 0;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e)
    {
        _ready.Start(e.StartTime, SpellUsable.CooldownRemaining(Spells.RadiantBlast.FSLID, e.StartTime) <= 0);
        if (!Owner.SelectedCombatant.HasBuff(Spells.AvatarOfLightBuff, e.StartTime)) return;

        _avatar = true;
        _ready.Exclude(e.StartTime);
    }

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
    private void OnCast()
    {
        CastCount++;
        if (_avatar) AvatarCasts++;
    }
}
