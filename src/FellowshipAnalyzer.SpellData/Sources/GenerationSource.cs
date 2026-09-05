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
    private readonly Dictionary<int, GenerationStatement> _statements;

    private GenerationSource(Dictionary<int, GenerationStatement> statements) => _statements = statements;

    /// <summary>What the description of <paramref name="id"/> states, or <see cref="GenerationStatement.None"/>.</summary>
    public GenerationStatement For(FSLID id) =>
        _statements.TryGetValue(id.Value, out var statement) ? statement : GenerationStatement.None;

    /// <summary>Reads every description in the export and keeps each one with a stated or unclaimed amount.</summary>
    public static GenerationSource Load(string entitiesPath)
    {
        var statements = new Dictionary<int, GenerationStatement>();

        foreach (var line in File.ReadLines(entitiesPath))
        {
            if (line.Length == 0)
                continue;

            var record = JsonSerializer.Deserialize<DescribedRecord>(line, Options)
                ?? throw new InvalidOperationException($"Could not read a record from '{entitiesPath}'.");

            if (record.Description is null || KindOf(record.Type) is not { } kind)
                continue;

            var statement = Generation.Read(record.Description);
            if (statement.Stated is not null || statement.Unclaimed.Count > 0)
                statements[FSLID.FromNative(kind, record.Id).Value] = statement;
        }

        return new GenerationSource(statements);
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
