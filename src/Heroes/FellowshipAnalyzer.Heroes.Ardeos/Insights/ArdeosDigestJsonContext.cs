using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Insights;

namespace FellowshipAnalyzer.Heroes.Ardeos.Insights;

/// <summary>
/// Serializes each Ardeos guide's <see cref="InsightMessage{TPull}"/> with camel-case names and without null
/// members, the shape the playbooks in <c>data/playbooks/ardeos</c> name.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InsightMessage<WildfirePull>))]
[JsonSerializable(typeof(InsightMessage<EngulfingFlamesPull>))]
[JsonSerializable(typeof(InsightMessage<DetonatePull>))]
[JsonSerializable(typeof(InsightMessage<SearingBlazePull>))]
public sealed partial class ArdeosDigestJsonContext : JsonSerializerContext;
