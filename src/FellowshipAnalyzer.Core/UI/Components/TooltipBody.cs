using System.Text.Json;

using Fellowship.SDK.Client;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Writes the body a tooltip is asked for with. The codex answers a tooltip to a <c>QUERY</c>
/// carrying the request as JSON, so a link states its request here and the tooltip module reads it
/// off the link and sends it.
/// </summary>
public static class TooltipBody
{
    /// <summary><paramref name="request"/> as the codex reads it back.</summary>
    public static string Write<TRequest>(TRequest request)
        where TRequest : class, ITooltipRequest =>
        JsonSerializer.Serialize(request, CodexJson.Options);
}
