using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>
/// The resource generation the game-data export states in description prose, keyed by FSLID.
/// <see cref="ExportSource"/> reads the export's structured columns; the sentences beside them are read
/// here and turned into <see cref="ResourceGeneration"/> by <see cref="SpellData.Generation"/>.
/// </summary>
public sealed class GenerationSource
{
    private readonly Dictionary<int, GenerationReading> _readings;

    private GenerationSource(Dictionary<int, GenerationReading> readings) => _readings = readings;

    /// <summary>What the description of <paramref name="id"/> states, or <see cref="GenerationReading.None"/>.</summary>
    public GenerationReading For(FSLID id) =>
        _readings.TryGetValue(id.Value, out var reading) ? reading : GenerationReading.None;

    /// <summary>Reads every description in the export and keeps the readings that found something.</summary>
    public static GenerationSource Load(string entitiesPath)
    {
        var readings = new Dictionary<int, GenerationReading>();

        foreach (var line in File.ReadLines(entitiesPath))
        {
            if (line.Length == 0)
                continue;

            var record = JsonSerializer.Deserialize<DescribedRecord>(line, Options)
                ?? throw new InvalidOperationException($"Could not read a record from '{entitiesPath}'.");

            if (record.Description is null || KindOf(record.Type) is not { } kind)
                continue;

            var reading = Generation.Read(record.Description);
            if (reading.Stated is not null || reading.Unclaimed.Count > 0)
                readings[FSLID.FromNative(kind, record.Id).Value] = reading;
        }

        return new GenerationSource(readings);
    }

    private static SpellKind? KindOf(string type) => type switch
    {
        "ability" => SpellKind.Ability,
        "effect" => SpellKind.Effect,
        "talent" => SpellKind.Talent,
        _ => null,
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class DescribedRecord
    {
        [JsonPropertyName("$type")]
        public string Type { get; init; } = string.Empty;
        public int Id { get; init; }
        public string? Description { get; init; }
    }
}
