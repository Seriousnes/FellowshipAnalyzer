namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// Maps a merged scalar dictionary to normalized spell scalars.
/// </summary>
public static class Normalization
{
    /// <summary>
    /// Extracts cooldown from scalars. Prefers "Cooldown", falls back to "RechargeTime".
    /// </summary>
    public static double? Cooldown(IReadOnlyDictionary<string, double> scalars)
    {
        if (scalars.TryGetValue("Cooldown", out var cooldown))
            return cooldown;
        if (scalars.TryGetValue("RechargeTime", out var rechargeTime))
            return rechargeTime;
        return null;
    }

    /// <summary>
    /// Extracts range from scalars. Returns MaxRange / 100 rounded to int, or null if not present.
    /// </summary>
    public static int? Range(IReadOnlyDictionary<string, double> scalars)
    {
        if (scalars.TryGetValue("MaxRange", out var maxRange))
            return (int)Math.Round(maxRange / 100);
        return null;
    }

    /// <summary>
    /// Extracts charges from scalars. Prefers "MaxCharges", falls back to "NumCharges", defaults to 1.
    /// </summary>
    public static int Charges(IReadOnlyDictionary<string, double> scalars)
    {
        if (scalars.TryGetValue("MaxCharges", out var maxCharges))
            return (int)Math.Round(maxCharges);
        if (scalars.TryGetValue("NumCharges", out var numCharges))
            return (int)Math.Round(numCharges);
        return 1;
    }

    /// <summary>
    /// Extracts cast duration from scalars. Prefers "CastingDuration", falls back to "CastTime".
    /// </summary>
    public static double? CastDuration(IReadOnlyDictionary<string, double> scalars)
    {
        if (scalars.TryGetValue("CastingDuration", out var castingDuration))
            return castingDuration;
        if (scalars.TryGetValue("CastTime", out var castTime))
            return castTime;
        return null;
    }

    /// <summary>
    /// Extracts channel duration from scalars.
    /// </summary>
    public static double? ChannelDuration(IReadOnlyDictionary<string, double> scalars)
    {
        if (scalars.TryGetValue("ChannelingDuration", out var channelingDuration))
            return channelingDuration;
        return null;
    }

    /// <summary>
    /// Extracts channel tick interval from scalars.
    /// </summary>
    public static double? ChannelTickInterval(IReadOnlyDictionary<string, double> scalars)
    {
        if (scalars.TryGetValue("ChannelingTickInterval", out var channelingTickInterval))
            return channelingTickInterval;
        return null;
    }
}
