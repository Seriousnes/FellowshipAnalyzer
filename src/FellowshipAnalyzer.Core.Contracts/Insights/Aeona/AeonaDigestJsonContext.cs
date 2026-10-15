using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Insights.Aeona;

/// <summary>
/// Reads and writes each Aeona guide's <see cref="InsightMessage{TPull}"/> with camel-case names and without null
/// members. Reading refuses a member the records do not declare.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(InsightMessage<EntropyClaimPull>))]
[JsonSerializable(typeof(InsightMessage<OblivionPull>))]
[JsonSerializable(typeof(InsightMessage<StaggerCleansePull>))]
[JsonSerializable(typeof(InsightMessage<ContinuumShiftPull>))]
[JsonSerializable(typeof(InsightMessage<KindRewindPull>))]
public sealed partial class AeonaDigestJsonContext : JsonSerializerContext;
