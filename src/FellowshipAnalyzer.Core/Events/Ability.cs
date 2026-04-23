using System.Diagnostics;
using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Events;

public interface ISpell
{
    int Id { get; }
    int Guid { get; }
}

[DebuggerDisplay("{Name,nq} - SpellId: {Guid}")]
public class Ability : ISpell
{
    [JsonIgnore]
    public virtual int Id { get => Guid; set => Guid = value; }

    [JsonPropertyName("guid")]
    public virtual int Guid { get; set; }

    public virtual string Name { get; set; } = string.Empty;

    [JsonPropertyName("abilityIcon")]
    public virtual string Icon { get; set; } = string.Empty;

    public virtual MagicSchool Type { get; set; }
}
