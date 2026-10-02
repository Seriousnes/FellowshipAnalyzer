using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public sealed class RuneSpenderCast(int timestamp, int spellId)
{
    private readonly HashSet<UnitKey> _targets = [];

    public int Timestamp { get; } = timestamp;

    public int SpellId { get; } = spellId;

    public int Targets => _targets.Count;

    internal void AddTarget(UnitKey target) => _targets.Add(target);
}

public sealed class RunicProliferationCast(int timestamp, int? runesBefore)
{
    private readonly List<RuneSpenderCast> _spenders = [];

    public int Timestamp { get; } = timestamp;

    public int? RunesBefore { get; } = runesBefore;

    public int? RunesLost => RunesBefore is { } held ? Math.Max(0, held + RunicProliferationAnalyzer.RunesGenerated - RunicProliferationAnalyzer.RuneCap) : null;

    public int? EndTimestamp { get; internal set; }

    public IReadOnlyList<RuneSpenderCast> Spenders => _spenders;

    public int SpenderCount => _spenders.Count;

    public int TargetsHit => _spenders.Sum(spender => spender.Targets);

    internal void AddSpender(RuneSpenderCast spender) => _spenders.Add(spender);
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class RunicProliferationAnalyzer : Analyzer
{
    public const int RunesGenerated = 3;
    public const int RuneCap = 6;
    public const int TargetLinkWindowMs = 150;

    private readonly List<RunicProliferationCast> _casts = [];
    private RuneSpenderCast? _lastSpender;
    private RunicProliferationCast? _open;

    public IReadOnlyList<RunicProliferationCast> Casts => _casts;

    public int CastCount => _casts.Count;

    public int SpenderCount => _casts.Sum(cast => cast.SpenderCount);

    public double? AverageSpenders => _casts.Count == 0 ? null : _casts.Average(cast => cast.SpenderCount);

    public double? AverageTargetsPerSpender => SpenderCount == 0 ? null : _casts.Sum(cast => cast.TargetsHit) / (double)SpenderCount;

    public int RunesLost => _casts.Sum(cast => cast.RunesLost ?? 0);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.RunicProliferation))]
    private void OnCast(CastEvent e)
    {
        var cast = new RunicProliferationCast(e.Timestamp, RunesHeld(e));
        _casts.Add(cast);
        _open = cast;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.RunicProliferationBuff))]
    private void OnBuffRemoved(RemoveBuffEvent e)
    {
        if (_open is null) return;

        _open.EndTimestamp = e.Timestamp;
        _open = null;
    }

    [On<CastEvent>(By = Actor.Player, Spells = new[] { nameof(Spells.Soulbrand), nameof(Spells.RuneOfRenewal), nameof(Spells.LuminousBarrier) })]
    private void OnSpender(CastEvent e)
    {
        _lastSpender = new RuneSpenderCast(e.Timestamp, e.Ability.Id);
        _open?.AddSpender(_lastSpender);
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SoulbrandDot))]
    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SoulbrandDot))]
    private void OnSoulbrandLanded(BuffEvent e) => Link(e, Spells.Soulbrand.FSLID);

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.RuneOfRenewalBuff))]
    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.RuneOfRenewalBuff))]
    private void OnRuneLanded(BuffEvent e) => Link(e, Spells.RuneOfRenewal.FSLID);

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.LuminousBarrierAbsorb))]
    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.LuminousBarrierAbsorb))]
    private void OnBarrierLanded(BuffEvent e) => Link(e, Spells.LuminousBarrier.FSLID);

    private void Link(BuffEvent e, int spellId)
    {
        if (_lastSpender is not { } spender || spender.SpellId != spellId) return;
        if (e.Timestamp - spender.Timestamp > TargetLinkWindowMs) return;

        spender.AddTarget(AuraWindowLedger.KeyOf(e));
    }

    private static int? RunesHeld(CastEvent e)
    {
        foreach (var resource in e.SourceResources?.Resources ?? [])
        {
            if (resource.Type == ResourceTypes.Primary) return resource.Amount;
        }

        return null;
    }
}
