using System.Text.Json;
using System.Text.Json.Nodes;

using FellowshipAnalyzer.Core.Insights;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>
/// Checks a <see cref="GuideInsightsRequest"/> before it reaches the model, so the endpoint answers only the
/// playbooks this build embeds, with short facts and no instructions of the caller's own.
/// </summary>
public static class InsightRequestValidation
{
    /// <summary>Longest string the user message may hold, property names included.</summary>
    public const int MaxStringLength = 64;

    /// <summary>A failed check: an HTTP status and message.</summary>
    public sealed record Failure(int StatusCode, string Error);

    /// <summary>Why <paramref name="request"/> may not go to the model, or null when it may.</summary>
    public static Failure? Check(string hero, string guide, GuideInsightsRequest? request, long maxUserBytes)
    {
        if (Playbook.Load(hero) is not { } playbook)
            return new Failure(404, $"No insights playbook for {hero}.");
        if (!playbook.Guides.ContainsKey(guide))
            return new Failure(404, $"No {hero} insights playbook for guide {guide}.");
        if (request is null || string.IsNullOrEmpty(request.System) || string.IsNullOrEmpty(request.User))
            return new Failure(400, "The request needs a system prompt and a user message.");
        if (!string.Equals(request.System, playbook.For(guide), StringComparison.Ordinal))
            return new Failure(400, "The insights playbook does not match this version of the app. Reload the page.");
        if (request.User.Length > maxUserBytes)
            return new Failure(413, "The user message is too large.");

        JsonObject? user;
        try
        {
            user = JsonNode.Parse(request.User) as JsonObject;
        }
        catch (JsonException)
        {
            user = null;
        }
        if (user is null)
            return new Failure(400, "The user message must be a JSON object.");
        if ((string?)user["guide"] != guide)
            return new Failure(400, "The user message names a different guide.");
        if (user["focus"] is not JsonValue focus || !focus.TryGetValue<string>(out var value) || !InsightFocus.All.Contains(value))
            return new Failure(400, "The user message has no known focus.");
        if (LongString(user) is { } text)
            return new Failure(400, $"The user message holds a string longer than {MaxStringLength} characters: {text[..MaxStringLength]}...");
        return null;
    }

    private static string? LongString(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Select(pair => pair.Key.Length > MaxStringLength ? pair.Key : LongString(pair.Value)).FirstOrDefault(text => text is not null),
        JsonArray array => array.Select(LongString).FirstOrDefault(text => text is not null),
        JsonValue scalar when scalar.TryGetValue<string>(out var text) && text.Length > MaxStringLength => text,
        _ => null,
    };
}
