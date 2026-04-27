using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Api.Core;

public sealed record RawEventsResult(byte[] JsonBytes, bool InProgress);

public sealed record AnalysisPreloadResponse(ReportInfoResponse ReportInfo, MasterDataResponse MasterData);

public sealed record ReportInfoResponse(
    string Code,
    string? Title,
    double StartTime,
    double? EndTime,
    IReadOnlyList<FightResponse> Fights,
    IReadOnlyList<ActorResponse> Actors
);

public sealed record FightResponse(
    int Id,
    string Name,
    int EncounterId,
    bool? Kill,
    double StartTime,
    double EndTime,
    int? Difficulty,
    IReadOnlyList<int>? FriendlyPlayers,
    double? FightPercentage,
    bool InProgress = false
);

public sealed record MasterDataResponse(
    IReadOnlyList<AbilityResponse> Abilities,
    IReadOnlyList<ActorResponse> Actors
);

public sealed record ActorResponse(
    int Id,
    string Name,
    string Type,
    string? SubType,
    string? Server,
    string? Icon
);

// Ability.Icon is serialized as "abilityIcon" in Core (JsonPropertyName attribute).
// This record mirrors that shape so the WASM can deserialize into Core's Ability type.
// Ability.Type is MagicSchool in Core; the WASM uses JsonStringEnumConverter(allowIntegerValues: true)
// so serializing as int here is compatible.
public sealed record AbilityResponse(
    int Guid,
    string Name,
    [property: JsonPropertyName("abilityIcon")] string Icon,
    int Type
);
