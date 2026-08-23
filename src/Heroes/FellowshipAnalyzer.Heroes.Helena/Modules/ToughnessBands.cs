namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public enum ToughnessBand
{
    Depleted,

    Level1,

    Level2,

    Level3,

    Level4,
}

public static class ToughnessBands
{
    public const double MaxToughnessStrengthScaler = 7.7;

    public static List<ToughnessBand> All { get; } =
        [ToughnessBand.Depleted, ToughnessBand.Level1, ToughnessBand.Level2, ToughnessBand.Level3, ToughnessBand.Level4];

    public static ToughnessBand For(double shareOfMaximum) => shareOfMaximum switch
    {
        >= 0.75 => ToughnessBand.Level4,
        >= 0.50 => ToughnessBand.Level3,
        >= 0.25 => ToughnessBand.Level2,
        >= 0.001 => ToughnessBand.Level1,
        _ => ToughnessBand.Depleted,
    };

    public static double LowerThreshold(ToughnessBand band) => band switch
    {
        ToughnessBand.Level4 => 0.75,
        ToughnessBand.Level3 => 0.50,
        ToughnessBand.Level2 => 0.25,
        ToughnessBand.Level1 => 0.001,
        _ => 0,
    };

    public const double FrontLineDefenderIncrease = 0.05;

    public static double DamageReduction(ToughnessBand band, bool frontLineDefender = false) => band switch
    {
        ToughnessBand.Level4 => 0.35 + (frontLineDefender ? FrontLineDefenderIncrease : 0),
        ToughnessBand.Level3 => 0.25 + (frontLineDefender ? FrontLineDefenderIncrease : 0),
        ToughnessBand.Level2 => 0.15 + (frontLineDefender ? FrontLineDefenderIncrease : 0),
        ToughnessBand.Level1 => 0.05 + (frontLineDefender ? FrontLineDefenderIncrease : 0),
        _ => 0,
    };

    public static double Ceiling(bool frontLineDefender) =>
        DamageReduction(ToughnessBand.Level4, frontLineDefender);

    public static double NominalGeneration(double strengthScaler) => strengthScaler / MaxToughnessStrengthScaler;
}
