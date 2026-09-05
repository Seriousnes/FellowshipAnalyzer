using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.SpellData.Json;

/// <summary>
/// Resolves upstream resource tokens (canonical <see cref="ResourceTypes"/> member names and
/// <see cref="ResourceNameAttribute"/> flavor aliases) to slots, and emits the camelCase member
/// name as the persisted token. Registered only on the offline spelldb/overrides options.
/// </summary>
public static class ResourceTypesAliases
{
    private static readonly Dictionary<string, ResourceTypes> ByToken = BuildAliasMap(EnumerateMembers());
    private static readonly Dictionary<ResourceTypes, string> ToTokenMap = BuildTokenMap();

    /// <summary>Every token that resolves to a slot, longest first, so a longest-match scan finds "Winter Orbs" before "Winter Orb".</summary>
    public static IReadOnlyList<string> Tokens { get; } =
        [.. ByToken.Keys.OrderByDescending(t => t.Length).ThenBy(t => t, StringComparer.Ordinal)];

    /// <summary>Resolves a token (member name or flavor alias, case-insensitive) to a slot.</summary>
    public static bool TryResolve(string? token, out ResourceTypes value)
    {
        if (token is not null && ByToken.TryGetValue(token, out value))
            return true;
        value = default;
        return false;
    }

    /// <summary>The persisted camelCase token for a slot (e.g. <c>tertiary</c>).</summary>
    public static string ToToken(ResourceTypes value) => ToTokenMap[value];

    /// <summary>
    /// Builds a case-insensitive token→slot map from canonical member names plus aliases,
    /// throwing if any token resolves to two different slots.
    /// </summary>
    public static Dictionary<string, ResourceTypes> BuildAliasMap(
        IEnumerable<(ResourceTypes Member, string[] Names)> members)
    {
        var map = new Dictionary<string, ResourceTypes>(StringComparer.OrdinalIgnoreCase);
        void Add(string token, ResourceTypes member)
        {
            if (map.TryGetValue(token, out var existing) && existing != member)
                throw new InvalidOperationException(
                    $"Resource token '{token}' resolves to both {existing} and {member}.");
            map[token] = member;
        }

        foreach (var (member, names) in members)
        {
            Add(member.ToString(), member);
            foreach (var name in names)
                Add(name, member);
        }
        return map;
    }

    private static IEnumerable<(ResourceTypes, string[])> EnumerateMembers()
    {
        foreach (ResourceTypes member in Enum.GetValues<ResourceTypes>())
        {
            var field = typeof(ResourceTypes).GetField(member.ToString())!;
            var names = field.GetCustomAttribute<ResourceNameAttribute>()?.Names ?? [];
            yield return (member, names);
        }
    }

    private static Dictionary<ResourceTypes, string> BuildTokenMap()
    {
        var map = new Dictionary<ResourceTypes, string>();
        foreach (ResourceTypes member in Enum.GetValues<ResourceTypes>())
        {
            var name = member.ToString();
            map[member] = char.ToLowerInvariant(name[0]) + name[1..];
        }
        return map;
    }
}

/// <summary>
/// System.Text.Json converter for <see cref="ResourceTypes"/> used as both a value and a
/// dictionary key in the offline spell-data files. Registered only on the SpellData/SpellStudio
/// options; never applied as a <c>[JsonConverter]</c> attribute on the enum.
/// </summary>
public sealed class ResourceTypesJsonConverter : JsonConverter<ResourceTypes>
{
    public override ResourceTypes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Resolve(reader.GetString());

    public override void Write(Utf8JsonWriter writer, ResourceTypes value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ResourceTypesAliases.ToToken(value));

    public override ResourceTypes ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Resolve(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ResourceTypes value, JsonSerializerOptions options) =>
        writer.WritePropertyName(ResourceTypesAliases.ToToken(value));

    private static ResourceTypes Resolve(string? token) =>
        ResourceTypesAliases.TryResolve(token, out var value)
            ? value
            : throw new JsonException($"Unknown resource token '{token}'.");
}
