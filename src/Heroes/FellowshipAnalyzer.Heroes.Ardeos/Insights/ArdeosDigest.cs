namespace FellowshipAnalyzer.Heroes.Ardeos.Insights;

/// <summary>Wildfire windows of one pull.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="Windows">Each Wildfire cast, in order.</param>
public sealed record WildfirePull(string Kind, List<WildfireWindowFacts> Windows);

/// <summary>One Wildfire cast and the casts around it.</summary>
/// <param name="DotsMissing">Tracked DoTs absent from the Wildfire target at the cast.</param>
/// <param name="EnoughDots">The target carried enough distinct DoTs for a complete setup.</param>
/// <param name="TwoEngulfingFlames">The target carried 2 Engulfing Flames instances.</param>
/// <param name="FollowUpComplete">Enough Detonate casts landed inside the Wildfire buff.</param>
/// <param name="DotCastAfter">A DoT was cast just after Wildfire.</param>
/// <param name="PyromaniaBefore">Pyromania cast just before Wildfire.</param>
/// <param name="IncinerateBefore">Incinerate cast just before Wildfire.</param>
/// <param name="CastsBefore">Fire casts just before Wildfire, in order.</param>
/// <param name="CastsAfter">Fire casts just after Wildfire, in order.</param>
public sealed record WildfireWindowFacts(
    List<string> DotsMissing,
    bool EnoughDots,
    bool TwoEngulfingFlames,
    bool FollowUpComplete,
    bool DotCastAfter,
    bool PyromaniaBefore,
    bool IncinerateBefore,
    List<string> CastsBefore,
    List<string> CastsAfter);

/// <summary>Engulfing Flames charges on one pull, and the setup before each Wildfire.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="WastedCharges">A recharge passed with Engulfing Flames at maximum charges.</param>
/// <param name="Setups">Each Wildfire setup, in order.</param>
public sealed record EngulfingFlamesPull(string Kind, bool WastedCharges, List<EngulfingFlamesSetupFacts> Setups);

/// <summary>Engulfing Flames casts in the setup before one Wildfire.</summary>
/// <param name="Setup"><c>full</c> with both charges cast, <c>partial</c> with one, <c>low</c> with none.</param>
/// <param name="FullChargesWhenWildfireReady">Engulfing Flames held maximum charges when Wildfire came off cooldown.</param>
public sealed record EngulfingFlamesSetupFacts(string Setup, bool FullChargesWhenWildfireReady);

/// <summary>Detonate casts on one pull.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="SurgeStacksExpired">Apocalyptic Surge stacks expired unused. Null without the talent.</param>
/// <param name="Casts">Each Detonate cast, in order.</param>
public sealed record DetonatePull(string Kind, bool? SurgeStacksExpired, List<DetonateCastFacts> Casts);

/// <summary>One Detonate cast.</summary>
/// <param name="DotStack"><c>full</c>, <c>partial</c> or <c>low</c>: the DoT instances on each enemy.</param>
/// <param name="DotsMissing">Tracked DoTs on no enemy at the cast.</param>
/// <param name="Free">Apocalyptic Surge made the cast free. Null without the talent.</param>
public sealed record DetonateCastFacts(string DotStack, List<string> DotsMissing, bool? Free);

/// <summary>Searing Blaze on one pull: uptime on a single boss, spread on a pack.</summary>
/// <param name="Kind"><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</param>
/// <param name="Applied">Searing Blaze landed at least once.</param>
/// <param name="Uptime"><c>full</c>, <c>partial</c> or <c>low</c> on the boss. Null on a pack.</param>
/// <param name="DroppedOff">Searing Blaze fell off the boss before a reapplication. Null on a pack.</param>
/// <param name="Spread"><c>full</c>, <c>partial</c> or <c>low</c> across the pack. Null on a boss.</param>
public sealed record SearingBlazePull(string Kind, bool Applied, string? Uptime, bool? DroppedOff, string? Spread);
