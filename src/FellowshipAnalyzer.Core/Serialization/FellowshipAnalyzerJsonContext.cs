using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Serialization;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Event))]
public partial class FellowshipAnalyzerJsonContext : JsonSerializerContext
{
}
