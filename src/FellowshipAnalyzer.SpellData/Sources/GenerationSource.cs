using System.Text.Json;

using Fellowship.SDK.Documents;

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

            if (Described(JsonSerializer.Deserialize<EntityDocument>(line, EntityDocuments.Options)) is not var (kind, id, description))
                continue;

            var statement = Generation.Read(description);
            if (statement.Stated is not null || statement.Unclaimed.Count > 0)
                statements[FSLID.FromNative(kind, id).Value] = statement;
        }

        return new GenerationSource(statements);
    }

    private static (SpellKind Kind, int Id, string Description)? Described(EntityDocument? document) => document switch
    {
        AbilityDocument { Description: { } text } ability => (SpellKind.Ability, ability.Id, text),
        EffectDocument { Description: { } text } effect => (SpellKind.Effect, effect.Id, text),
        TalentDocument { Description: { } text } talent => (SpellKind.Talent, talent.Id, text),
        _ => null,
    };
}
