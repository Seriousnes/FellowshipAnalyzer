namespace FellowshipAnalyzer.Api.Core;

/// <summary>
/// Declares an HTTP API endpoint on a method. The <see cref="FellowshipAnalyzer.Generators"/>
/// source generator scans for this attribute and emits per-host registration code:
/// Minimal API mapping (DevApi) and Azure Functions Worker [Function] wrappers (Api).
/// </summary>
/// <param name="method">HTTP method, e.g. "GET", "POST".</param>
/// <param name="route">
/// Route template without the <c>/api</c> prefix. Route tokens (<c>{name}</c>)
/// must match a parameter name on the decorated method.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ApiEndpointAttribute(string method, string route) : Attribute
{
    public string Method { get; } = method;
    public string Route { get; } = route;
}
