using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Reads an <see cref="FSLID"/> from either a namespaced number (the combat-log <c>guid</c>) or a
/// native object <c>{ "id": &lt;nativeId&gt;, "kind": "ability|effect|talent|weapon" }</c>. Writes the
/// namespaced <see cref="FSLID.Value"/> as a number.
/// </summary>
public sealed class FSLIDJsonConverter : JsonConverter<FSLID>
{
    public override FSLID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return new FSLID(reader.GetInt32());

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int nativeId = 0;
            SpellKind kind = SpellKind.Ability;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return FSLID.FromNative(kind, nativeId);
                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;
                string? property = reader.GetString();
                reader.Read();
                if (string.Equals(property, "id", StringComparison.OrdinalIgnoreCase))
                    nativeId = reader.GetInt32();
                else if (string.Equals(property, "kind", StringComparison.OrdinalIgnoreCase))
                    kind = ParseKind(reader.GetString());
            }
            throw new JsonException("Unterminated FSLID object.");
        }

        throw new JsonException($"Unexpected token {reader.TokenType} reading FSLID.");
    }

    public override void Write(Utf8JsonWriter writer, FSLID value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);

    private static SpellKind ParseKind(string? kind) => kind?.ToLowerInvariant() switch
    {
        "effect" => SpellKind.Effect,
        "talent" => SpellKind.Talent,
        "weapon" => SpellKind.Weapon,
        _ => SpellKind.Ability,
    };
}
