using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Insights.Ardeos;

/// <summary>
/// Reads and writes each Ardeos guide's <see cref="InsightMessage{TPull}"/> with camel-case names and without null
/// members. Reading refuses a member the records do not declare.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(InsightMessage<WildfirePull>))]
[JsonSerializable(typeof(InsightMessage<EngulfingFlamesPull>))]
[JsonSerializable(typeof(InsightMessage<DetonatePull>))]
[JsonSerializable(typeof(InsightMessage<SearingBlazePull>))]
public sealed partial class ArdeosDigestJsonContext : JsonSerializerContext;
