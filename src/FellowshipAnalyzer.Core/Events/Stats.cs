namespace FellowshipAnalyzer.Core.Events;

public class Stats
{
    public double? Intellect { get; set; }
    public double? Stamina { get; set; }
    public double? Armor { get; set; }
    public double? Crit { get; set; }
    public double? Haste { get; set; }
    public double? Expertise { get; set; }
    public double? Spirit { get; set; }

    public static Stats operator -(Stats a, Stats b) => new()
    {
        Intellect = (a.Intellect ?? 0) - (b.Intellect ?? 0),
        Stamina = (a.Stamina ?? 0) - (b.Stamina ?? 0),
        Armor = (a.Armor ?? 0) - (b.Armor ?? 0),
        Crit = (a.Crit ?? 0) - (b.Crit ?? 0),
        Haste = (a.Haste ?? 0) - (b.Haste ?? 0),
        Expertise = (a.Expertise ?? 0) - (b.Expertise ?? 0),
        Spirit = (a.Spirit ?? 0) - (b.Spirit ?? 0),
    };
}
