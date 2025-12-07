using System.Diagnostics;

namespace FellowshipAnalyzer.Core.Events;

[DebuggerDisplay("{Name,nq}")]
public class Unit
{
    public Unit() { }

    public Unit(int id, string name, int guid, UnitTypeEnum type, string icon)
    {
        Id = id;
        Name = name;
        Guid = guid;
        Type = type;
        Icon = icon;
    }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Guid { get; set; }
    public UnitTypeEnum Type { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public static class DefaultActors
{
    public static Unit Environment => new(-1, "Environment", 0, UnitTypeEnum.NPC, "NPC");
}
