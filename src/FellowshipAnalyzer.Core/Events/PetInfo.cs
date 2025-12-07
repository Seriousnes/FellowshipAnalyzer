namespace FellowshipAnalyzer.Core.Events;

public class PetInfo : Unit
{
    public int PetOwner { get; set; }
    public List<PetFight> Fights { get; set; } = [];
}

public class PetFight
{
    public int Id { get; set; }
    public int Instances { get; set; }
}
