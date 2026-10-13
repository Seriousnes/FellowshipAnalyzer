using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Insights;

namespace FellowshipAnalyzer.Heroes.Aeona.Insights;

/// <summary>
/// Serializes each Aeona guide's <see cref="InsightMessage{TPull}"/> with camel-case names and without null
/// members, the shape the playbooks in <c>data/playbooks/aeona</c> name.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InsightMessage<EntropyClaimPull>))]
[JsonSerializable(typeof(InsightMessage<OblivionPull>))]
[JsonSerializable(typeof(InsightMessage<StaggerCleansePull>))]
[JsonSerializable(typeof(InsightMessage<ContinuumShiftPull>))]
[JsonSerializable(typeof(InsightMessage<KindRewindPull>))]
public sealed partial class AeonaDigestJsonContext : JsonSerializerContext;
