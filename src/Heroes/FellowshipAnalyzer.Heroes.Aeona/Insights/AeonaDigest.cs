namespace FellowshipAnalyzer.Heroes.Aeona.Insights;

/// <summary>Entropy's Claim casts and Entropic Burst on one pull.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="LapsedWithChargeReady">Entropic Burst lapsed while an Entropy's Claim charge was ready to roll it over.</param>
/// <param name="Casts">Each Entropy's Claim cast, in order.</param>
public sealed record EntropyClaimPull(string Kind, bool LapsedWithChargeReady, List<EntropyClaimCastFacts> Casts);

/// <summary>One Entropy's Claim cast.</summary>
/// <param name="Timing">
/// <c>early</c>, <c>in window</c> or <c>late</c> against the window before the active Entropy's Claim expires,
/// or <c>none active</c>.
/// </param>
/// <param name="RolledOver">Its expiry added an Entropic Burst stack.</param>
public sealed record EntropyClaimCastFacts(string Timing, bool RolledOver);

/// <summary>Oblivion casts on one pull.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="CastWhileCleanseDue">
/// <c>never</c>, <c>sometimes</c> or <c>often</c>: Oblivion cast with the tank above the cleanse line and a
/// cleanse available.
/// </param>
public sealed record OblivionPull(string Kind, string CastWhileCleanseDue);

/// <summary>Amend Fate and Restore Continuity casts on one pull.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="Casts">Each cast, in order.</param>
public sealed record StaggerCleansePull(string Kind, List<CleanseCastFacts> Casts);

/// <summary>One Amend Fate or Restore Continuity cast.</summary>
/// <param name="Spell">Amend Fate or Restore Continuity.</param>
/// <param name="Free">Uchronia or Epoch Break for a free cast. Null when it cost Chrona.</param>
/// <param name="TankUnderCleanseLine">The tank held under the cleanse line of Stagger. Null when unknown.</param>
/// <param name="TargetHeldLessThanOneCast">The target held less Stagger than one cast removes. Null when unknown.</param>
/// <param name="HealedLessThanOblivion">The cast healed less than one Oblivion. Null with no Oblivion on the pull.</param>
/// <param name="AppliedEchoes">The cast applied Echoes of Divinity to the tank. Null without the talent.</param>
/// <param name="RefreshedRunningEchoes">The cast refreshed Echoes of Divinity still running on the tank. Null without the talent.</param>
public sealed record CleanseCastFacts(
    string Spell,
    string? Free,
    bool? TankUnderCleanseLine,
    bool? TargetHeldLessThanOneCast,
    bool? HealedLessThanOblivion,
    bool? AppliedEchoes,
    bool? RefreshedRunningEchoes);

/// <summary>Continuum Shift windows on one pull.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="Shifts">Each Continuum Shift, in order.</param>
public sealed record ContinuumShiftPull(string Kind, List<ContinuumShiftFacts> Shifts);

/// <summary>One Continuum Shift and the cast that spent it.</summary>
/// <param name="SpentOn">Time Shard, Echoes of Ruin, Entropy's Claim, <c>nothing</c> or <c>open at pull end</c>.</param>
/// <param name="TankInDanger">Echoes of Ruin: the tank was in danger. Null otherwise.</param>
/// <param name="FleetingHourFollowed">Echoes of Ruin with Edge of Ruin: Fleeting Hour followed. Null otherwise.</param>
/// <param name="ChronaOvercapped">Time Shard: the cast generated Chrona above maximum. Null otherwise.</param>
public sealed record ContinuumShiftFacts(string SpentOn, bool? TankInDanger, bool? FleetingHourFollowed, bool? ChronaOvercapped);

/// <summary>Time Shards with Kind Rewind on one pull.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="TimeShardsWithoutEchoesOfRuin">A Time Shard went toward an enemy without Echoes of Ruin.</param>
public sealed record KindRewindPull(string Kind, bool TimeShardsWithoutEchoesOfRuin);
