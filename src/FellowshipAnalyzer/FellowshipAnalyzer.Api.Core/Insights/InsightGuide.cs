using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

using FellowshipAnalyzer.Api.Core.Insights.Prompts;
using FellowshipAnalyzer.Core.Insights;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>A user message <see cref="InsightGuide.Read"/> wrote from a request body.</summary>
/// <param name="Guide">Guide the message names.</param>
/// <param name="Focus">Focus the message names.</param>
/// <param name="User">The typed message serialized again.</param>
/// <param name="Kinds">Kinds of pull the message's patterns name.</param>
public sealed record InsightUserMessage(string Guide, string Focus, string User, IReadOnlyList<string> Kinds);

/// <summary>
/// One guide with insights: its system prompt, and the user message it writes from a request body. Every
/// string in that message is a fixed digest value, a name in <see cref="SpellNames"/>, or a name the prompt
/// writes in backticks.
/// </summary>
public abstract partial class InsightGuide
{
    private readonly HashSet<string> _values;
    private readonly IReadOnlyList<Regex> _templates;

    private protected InsightGuide(string hero, string guide, string heroOverview, string rules, IEnumerable<string> heroValues)
    {
        Hero = hero;
        Guide = guide;
        Overview = Part(InsightPrompts.Overview) + "\n" + Part(heroOverview);
        System = Overview + "\n" + Part(rules);
        var names = BacktickedName().Matches(System).Select(match => match.Groups["name"].Value).ToList();
        _values = [guide, .. DigestValues.All, .. heroValues, .. SpellNames.All, .. names.Where(name => !Placeholder().IsMatch(name))];
        _templates = [.. names.Where(name => Placeholder().IsMatch(name)).Distinct(StringComparer.Ordinal).Select(Template)];
    }

    /// <summary>Hero name, lower case.</summary>
    public string Hero { get; }

    /// <summary>Guide name, as the hero's prompts and the user message name it.</summary>
    public string Guide { get; }

    /// <summary>The shared rules and the hero's: the message conventions, the Output rules and the hero's example.</summary>
    public string Overview { get; }

    /// <summary>System prompt of the guide's call: <see cref="Overview"/>, then the guide's rules.</summary>
    public string System { get; }

    /// <summary>
    /// The user message written from <paramref name="body"/>, or null when the body is not a message of this
    /// guide's shape. The message is the typed message serialized again, so no member of the caller's own
    /// reaches the model.
    /// </summary>
    public abstract InsightUserMessage? Read(ReadOnlySpan<byte> body);

    /// <summary>
    /// The first string in <paramref name="message"/> that is no value of this guide, or the first kind of pull
    /// that is none of <see cref="DigestValues.Kinds"/>; null with neither.
    /// </summary>
    public string? UnknownValue(InsightUserMessage message) =>
        Strings(JsonNode.Parse(message.User)).FirstOrDefault(text => !IsValue(text))
        ?? message.Kinds.FirstOrDefault(kind => !DigestValues.Kinds.Contains(kind));

    [GeneratedRegex("`(?<name>[^`]+)`")]
    private static partial Regex BacktickedName();

    [GeneratedRegex(@"\{[A-Za-z]+\}")]
    private static partial Regex Placeholder();

    private bool IsValue(string text) =>
        _values.Contains(text) || _templates.Any(template => template.Match(text) is { Success: true } match
            && match.Groups.Values.Skip(1).All(group => SpellNames.All.Contains(group.Value)));

    private static Regex Template(string name) =>
        new("^" + string.Join("(.+)", Placeholder().Split(name).Select(Regex.Escape)) + "$", RegexOptions.CultureInvariant);

    private static IEnumerable<string> Strings(JsonNode? node) => node switch
    {
        JsonObject obj => obj.SelectMany(pair => Strings(pair.Value)),
        JsonArray array => array.SelectMany(Strings),
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => [text],
        _ => [],
    };

    private static string Part(string text) => text.ReplaceLineEndings("\n").TrimEnd() + "\n";
}

/// <summary>An <see cref="InsightGuide"/> whose message has one <typeparamref name="TPull"/> per pull.</summary>
public sealed class InsightGuide<TPull>(
    string hero,
    string guide,
    string heroOverview,
    string rules,
    IEnumerable<string> heroValues,
    JsonTypeInfo<InsightMessage<TPull>> type) : InsightGuide(hero, guide, heroOverview, rules, heroValues)
{
    public override InsightUserMessage? Read(ReadOnlySpan<byte> body)
    {
        InsightMessage<TPull>? message;
        try
        {
            message = JsonSerializer.Deserialize(body, type);
        }
        catch (JsonException)
        {
            return null;
        }
        if (message?.Patterns is null || message.Patterns.Any(pattern => pattern?.Often is null))
            return null;
        return new InsightUserMessage(
            message.Guide,
            message.Focus,
            JsonSerializer.Serialize(message, type),
            [.. message.Patterns.SelectMany(pattern => pattern.Often.Keys)]);
    }
}
