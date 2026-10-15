using FellowshipAnalyzer.Core.Insights;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>
/// Checks an insights request body before it reaches the model. The body is a guide's insight message: the
/// endpoint answers only the guides of <see cref="InsightGuides"/>, with that guide's own system prompt, and
/// every string in the message is a value the guide knows.
/// </summary>
public static class InsightRequestValidation
{
    private const int ShownValueLength = 64;

    /// <summary>A failed check: an HTTP status and message.</summary>
    public sealed record Failure(int StatusCode, string Error);

    /// <summary>A body that may go to the model: its guide and the user message written from it.</summary>
    public sealed record Accepted(InsightGuide Guide, string User);

    /// <summary>The guide and user message of <paramref name="body"/>, or why it may not go to the model.</summary>
    public static (Accepted? Accepted, Failure? Failure) Check(string hero, string guide, ReadOnlySpan<byte> body)
    {
        if (InsightGuides.Find(hero, guide) is not { } entry)
            return (null, new Failure(404, $"No {hero} insights for guide {guide}."));
        if (entry.Read(body) is not { } message)
            return (null, new Failure(400, $"The body must be a {hero} {guide} insight message."));
        if (message.Guide != guide)
            return (null, new Failure(400, "The message names a different guide."));
        if (!InsightFocus.All.Contains(message.Focus))
            return (null, new Failure(400, "The message has no known focus."));
        if (entry.UnknownValue(message) is { } value)
            return (null, new Failure(400, $"The message holds a value the guide does not know: {(value.Length > ShownValueLength ? value[..ShownValueLength] + "..." : value)}"));
        return (new Accepted(entry, message.User), null);
    }
}
