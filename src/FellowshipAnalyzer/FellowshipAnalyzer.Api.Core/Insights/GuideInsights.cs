using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

using Anthropic.Models.Messages;

using Microsoft.Extensions.AI;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>Sends one guide's insights request to a model and parses the structured response.</summary>
public static class GuideInsights
{
    /// <summary>Output token ceiling for one call.</summary>
    public const int MaxOutputTokens = 4_000;

    /// <summary>The structured response every call must match.</summary>
    public static JsonElement ResponseSchema { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["insights"],
          "properties": {
            "insights": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["title", "insight"],
                "properties": {
                  "title": { "type": "string" },
                  "insight": { "type": "string" }
                }
              }
            }
          }
        }
        """).RootElement;

    /// <summary>Sends <paramref name="user"/> with <paramref name="system"/> as the cached system prompt.</summary>
    public static async Task<InsightResult> RunAsync(
        IChatClient chat, string model, string effort, string system, string user, CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, [new TextContent(system).WithCacheControl(new CacheControlEphemeral())]),
            new(ChatRole.User, user),
        };
        var options = new ChatOptions
        {
            ModelId = model,
            MaxOutputTokens = MaxOutputTokens,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(ResponseSchema, "insights", "Insights for one guide."),
            Reasoning = new ReasoningOptions { Effort = effort switch
            {
                "low" => ReasoningEffort.Low,
                "medium" => ReasoningEffort.Medium,
                _ => ReasoningEffort.High,
            } },
        };

        var stopwatch = Stopwatch.StartNew();
        ChatResponse response;
        Exception? error = null;
        try
        {
            response = await chat.GetResponseAsync(messages, options, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = ex;
            response = new ChatResponse();
        }
        stopwatch.Stop();

        var text = error is null ? response.Text : "";
        JsonNode? parsed = null;
        var parseError = error?.Message;
        if (error is null)
        {
            try
            {
                parsed = JsonNode.Parse(text);
            }
            catch (JsonException ex)
            {
                parseError = ex.Message;
            }
        }

        return new InsightResult(model, parsed, parseError, ModelUsage.From(response.RawRepresentation as Message, response.Usage), stopwatch.Elapsed);
    }
}

/// <summary>Token counts of one call.</summary>
public sealed record ModelUsage(long Input, long CacheRead, long CacheWrite, long Output)
{
    /// <summary>Usage from the Anthropic message when present, otherwise from the chat client's usage details.</summary>
    public static ModelUsage From(Message? raw, UsageDetails? details)
    {
        if (raw?.Usage is { } usage)
            return new ModelUsage(usage.InputTokens, usage.CacheReadInputTokens ?? 0, usage.CacheCreationInputTokens ?? 0, usage.OutputTokens);
        var cached = details?.CachedInputTokenCount ?? 0;
        return new ModelUsage((details?.InputTokenCount ?? 0) - cached, cached, 0, details?.OutputTokenCount ?? 0);
    }

    /// <summary>Cost in US dollars at <paramref name="price"/>, cache reads and writes at their own rates.</summary>
    public double Cost(ModelPrice price) =>
        (Input * price.Input + CacheWrite * price.CacheWrite + CacheRead * price.CacheRead + Output * price.Output) / 1_000_000;
}

/// <summary>Price per million tokens.</summary>
public sealed record ModelPrice(double Input, double Output, double CacheWrite, double CacheRead)
{
    /// <summary>
    /// Anthropic list price of <paramref name="model"/> as of 2026-10-06, or null for a model without one. A cache write
    /// is 1.25 times input (5-minute TTL).
    /// </summary>
    public static ModelPrice? For(string model) => model switch
    {
        "claude-haiku-5-5" => new ModelPrice(0.10, 0.50, 0.125, 0.01),
        "claude-sonnet-5-5" => new ModelPrice(2, 10, 2.50, 0.20),
        "claude-opus-5-5" => new ModelPrice(4, 20, 5, 0.20),
        _ => null,
    };
}

/// <summary>One call's response, before validation.</summary>
public sealed record InsightResult(string Model, JsonNode? Parsed, string? Error, ModelUsage Usage, TimeSpan Latency)
{
    /// <summary>Cost in US dollars, or null for a model without a price.</summary>
    public double? CostUsd => ModelPrice.For(Model) is { } price ? Usage.Cost(price) : null;
}
