using System.Diagnostics;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>A player, NPC, or pet as identified in the FellowshipLogs report's actor list.</summary>
[DebuggerDisplay("{Name,nq}")]
public class Unit
{
    /// <summary>Creates an empty unit for deserialization.</summary>
    public Unit() { }

    /// <summary>Creates a unit with the given identity and display details.</summary>
    public Unit(int id, string name, int guid, UnitTypeEnum type, string icon)
    {
        Id = id;
        Name = name;
        Guid = guid;
        Type = type;
        Icon = icon;
    }

    /// <summary>Unique identifier for this unit within the report's actor list.</summary>
    public int Id { get; set; }
    /// <summary>The unit's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The game's persistent identifier for this unit, shared across reports.</summary>
    public int Guid { get; set; }
    /// <summary>Whether this unit is a player, NPC, or pet.</summary>
    public UnitTypeEnum Type { get; set; }
    /// <summary>Icon name used to render this unit in the UI.</summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>Well-known units that are not part of the report's actor list.</summary>
public static class DefaultActors
{
    /// <summary>The pseudo-unit representing environmental damage sources, such as fall damage or hazards.</summary>
    public static Unit Environment => new(-1, "Environment", 0, UnitTypeEnum.NPC, "NPC");
}
