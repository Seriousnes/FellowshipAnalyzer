using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed record HemorrhageApplication(int Timestamp, int ComboPoints, int ExpectedDurationMs, bool Refresh);

public sealed record HemorrhageRefresh(int Timestamp, int RemainingMs);

[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class MaraDotUptimeAnalyzer : DotUptimeAnalyzer, IMaraDotAnalyzer
{
    public const int HemorrhageBaseDurationMs = 12_000;

    public const int HemorrhageComboPointDurationMs = 3_000;

    private readonly Dictionary<(int TargetId, int TargetInstance), int> _hemorrhageExpiry = [];
    private readonly List<HemorrhageApplication> _hemorrhageApplications = [];
    private readonly List<HemorrhageRefresh> _hemorrhageRefreshes = [];

    private int _comboPointsOnStrike;

    protected override List<Dot> Dots => MaraDots.Maintained;

    public DotUptime SeethingPoison => For(MaraDots.SeethingPoison);

    public DotUptime Hemorrhage => For(MaraDots.Hemorrhage);

    public List<HemorrhageApplication> HemorrhageApplications => _hemorrhageApplications;

    public List<HemorrhageRefresh> HemorrhageRefreshes => _hemorrhageRefreshes;

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HemorrhagingStrike))]
    private void OnHemorrhagingStrike(CastEvent e)
    {
        var resources = e.SourceResources?.Resources;
        if (resources is null) return;

        foreach (var resource in resources)
        {
            if (resource.Type != ResourceTypes.Secondary) continue;

            _comboPointsOnStrike = resource.Amount;
            return;
        }
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.HemorrhagingStrikeBleed)])]
    private void OnApplied(ApplyDebuffEvent e)
    {
        OpenWindow(e);
        TrackHemorrhage(e, refresh: false);
    }

    [On<RefreshDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.HemorrhagingStrikeBleed)])]
    private void OnRefreshed(RefreshDebuffEvent e)
    {
        OpenWindow(e);
        TrackHemorrhage(e, refresh: true);
    }

    [On<RemoveDebuffEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.HemorrhagingStrikeBleed)])]
    private void OnRemoved(RemoveDebuffEvent e)
    {
        CloseWindow(e);

        if (e.Ability.Id == MaraDots.Hemorrhage.EffectId)
            _hemorrhageExpiry.Remove((e.TargetId, e.TargetInstance ?? 0));
    }

    [On<DamageEvent>(By = Actor.Player, Spells = [nameof(Spells.WidowBitePoison), nameof(Spells.HemorrhagingStrikeBleed)])]
    private void OnTicked(DamageEvent e) => ObserveTarget(e);

    private void TrackHemorrhage(BuffEvent e, bool refresh)
    {
        if (e.Ability.Id != MaraDots.Hemorrhage.EffectId) return;

        var target = (e.TargetId, e.TargetInstance ?? 0);
        if (refresh)
        {
            var remaining = _hemorrhageExpiry.TryGetValue(target, out var expiry)
                ? Math.Max(0, expiry - e.Timestamp)
                : 0;
            _hemorrhageRefreshes.Add(new HemorrhageRefresh(e.Timestamp, remaining));
        }

        var duration = HemorrhageBaseDurationMs + (HemorrhageComboPointDurationMs * _comboPointsOnStrike);
        _hemorrhageApplications.Add(new HemorrhageApplication(e.Timestamp, _comboPointsOnStrike, duration, refresh));
        _hemorrhageExpiry[target] = e.Timestamp + duration;
    }
}
