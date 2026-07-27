namespace FellowshipAnalyzer.Core.Events;

/// <summary>A summoned pet unit, as reported by FellowshipLogs; typically the target of a <see cref="SummonEvent"/>.</summary>
public class PetInfo : Unit
{
    /// <summary>The unit id of the player who owns this pet.</summary>
    public int PetOwner { get; set; }
    /// <summary>The fights FellowshipLogs recorded this pet as present in.</summary>
    public List<PetFight> Fights { get; set; } = [];
}

/// <summary>One fight a pet appeared in, as reported by FellowshipLogs.</summary>
public class PetFight
{
    /// <summary>The FellowshipLogs fight id this record refers to.</summary>
    public int Id { get; set; }
    /// <summary>The number of simultaneous instances of the pet present during this fight.</summary>
    public int Instances { get; set; }
}
