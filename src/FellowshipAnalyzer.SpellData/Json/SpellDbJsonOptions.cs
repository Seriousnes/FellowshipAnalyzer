using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.SpellData.Json;

/// <summary>
/// The System.Text.Json options shared by <c>spelldb.json</c> and <c>overrides.json</c>:
/// camelCase names, null fields omitted, the <see cref="ResourceTypesJsonConverter"/> for costs,
/// the string form of <see cref="AbilityCategory"/> and <see cref="MagicSchool"/>, and polymorphic
/// <c>Spell</c> serialization driven by the attributes on the type. Used only by the offline tooling;
/// never registered on the runtime combat-log path.
/// </summary>
public static class SpellDbJsonOptions
{
    /// <summary>The reflection-based options for reading and writing curated spell data.</summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new ResourceTypesJsonConverter(),
            new JsonStringEnumConverter<AbilityCategory>(),
            new JsonStringEnumConverter<MagicSchool>(),
            new JsonStringEnumConverter<GenerationMeasure>(),
            new JsonStringEnumConverter<GenerationTrigger>(),
        },
    };
}
