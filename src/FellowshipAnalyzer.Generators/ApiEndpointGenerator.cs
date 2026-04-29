using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FellowshipAnalyzer.Generators;

/// <summary>
/// Discovers methods decorated with <c>[ApiEndpoint]</c> across the source assembly and
/// the assembly containing the attribute, and emits per-host endpoint registration code.
/// The host kind is selected via the <c>FellowshipApiHostKind</c> MSBuild property
/// (one of: <c>MinimalApi</c>, <c>Functions</c>).
/// </summary>
[Generator]
public sealed class ApiEndpointGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "FellowshipAnalyzer.Api.Core.ApiEndpointAttribute";
    private const string HostKindProperty = "build_property.FellowshipApiHostKind";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hostKindProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                options.GlobalOptions.TryGetValue(HostKindProperty, out var v) && !string.IsNullOrWhiteSpace(v)
                    ? v.Trim()
                    : null);

        var endpointsProvider = context.CompilationProvider
            .Select(static (compilation, ct) => CollectEndpoints(compilation, ct));

        var combined = endpointsProvider.Combine(hostKindProvider);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var endpoints = pair.Left;
            var hostKind = pair.Right;
            if (endpoints.IsDefaultOrEmpty || string.IsNullOrEmpty(hostKind))
            {
                return;
            }

            if (string.Equals(hostKind, "MinimalApi", System.StringComparison.OrdinalIgnoreCase))
            {
                EmitMinimalApi(spc, endpoints);
            }
            else if (string.Equals(hostKind, "Functions", System.StringComparison.OrdinalIgnoreCase))
            {
                EmitFunctions(spc, endpoints);
            }
        });
    }

    private static ImmutableArray<EndpointInfo> CollectEndpoints(Compilation compilation, CancellationToken ct)
    {
        var attributeSymbol = compilation.GetTypeByMetadataName(AttributeFullName);
        if (attributeSymbol is null)
        {
            return ImmutableArray<EndpointInfo>.Empty;
        }

        var visited = new HashSet<string>(System.StringComparer.Ordinal);
        var results = ImmutableArray.CreateBuilder<EndpointInfo>();

        ScanAssembly(compilation.Assembly, attributeSymbol, results, visited, ct);
        ScanAssembly(attributeSymbol.ContainingAssembly, attributeSymbol, results, visited, ct);

        return results.ToImmutable();
    }

    private static void ScanAssembly(
        IAssemblySymbol assembly,
        INamedTypeSymbol attributeSymbol,
        ImmutableArray<EndpointInfo>.Builder results,
        HashSet<string> visited,
        CancellationToken ct)
    {
        if (!visited.Add(assembly.Identity.GetDisplayName()))
        {
            return;
        }

        foreach (var type in EnumerateTypes(assembly.GlobalNamespace))
        {
            ct.ThrowIfCancellationRequested();
            foreach (var member in type.GetMembers())
            {
                if (member is not IMethodSymbol method)
                {
                    continue;
                }

                AttributeData? attr = null;
                foreach (var candidate in method.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeSymbol))
                    {
                        attr = candidate;
                        break;
                    }
                }

                if (attr is null || attr.ConstructorArguments.Length < 2)
                {
                    continue;
                }

                var httpMethod = attr.ConstructorArguments[0].Value as string;
                var route = attr.ConstructorArguments[1].Value as string;
                if (string.IsNullOrWhiteSpace(httpMethod) || route is null)
                {
                    continue;
                }

                results.Add(EndpointInfo.From(method, type, httpMethod!, route));
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
        {
            yield return t;
            foreach (var nested in EnumerateNested(t))
            {
                yield return nested;
            }
        }
        foreach (var sub in ns.GetNamespaceMembers())
        {
            foreach (var t in EnumerateTypes(sub))
            {
                yield return t;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNested(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in EnumerateNested(nested))
            {
                yield return deeper;
            }
        }
    }

    // ---------- MinimalApi emission ----------

    private static void EmitMinimalApi(SourceProductionContext spc, ImmutableArray<EndpointInfo> endpoints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Routing;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("namespace FellowshipAnalyzer.Api.Core.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class FellowshipApiEndpointsExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Registers all [ApiEndpoint]-decorated methods under the <c>/api</c> route group.</summary>");
        sb.AppendLine("    public static IEndpointRouteBuilder MapFellowshipApiEndpoints(this IEndpointRouteBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine("        var group = builder.MapGroup(\"/api\");");
        foreach (var ep in endpoints)
        {
            EmitMinimalApiMapping(sb, ep);
        }
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("FellowshipApiEndpointsExtensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void EmitMinimalApiMapping(StringBuilder sb, EndpointInfo ep)
    {
        // Build lambda parameters mirroring the handler method, plus the handler injected from DI.
        var lambdaParams = new List<string> { $"[Microsoft.AspNetCore.Mvc.FromServices] {ep.ContainingTypeFullName} __handler" };
        var callArgs = new List<string>();
        foreach (var p in ep.Parameters)
        {
            lambdaParams.Add($"{p.TypeFullName} {p.Name}");
            callArgs.Add(p.Name);
        }

        sb.Append("        group.MapMethods(\"")
            .Append(EscapeForString(ep.Route))
            .Append("\", new[] { \"")
            .Append(EscapeForString(ep.HttpMethod.ToUpperInvariant()))
            .Append("\" }, (")
            .Append(string.Join(", ", lambdaParams))
            .Append(") => __handler.")
            .Append(ep.MethodName)
            .Append('(')
            .Append(string.Join(", ", callArgs))
            .AppendLine("));");
    }

    // ---------- Functions emission ----------

    private static void EmitFunctions(SourceProductionContext spc, ImmutableArray<EndpointInfo> endpoints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Microsoft.Azure.Functions.Worker;");
        sb.AppendLine();
        sb.AppendLine("namespace FellowshipAnalyzer.Api.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal sealed class FellowshipApiFunctions");
        sb.AppendLine("{");

        // Group endpoints by containing type so we can inject one handler per type.
        var byType = endpoints
            .GroupBy(e => e.ContainingTypeFullName)
            .OrderBy(g => g.Key, System.StringComparer.Ordinal)
            .ToList();

        // Constructor injects all distinct handler types.
        var ctorParams = string.Join(", ", byType.Select((g, i) => $"{g.Key} handler{i}"));
        sb.Append("    public FellowshipApiFunctions(").Append(ctorParams).AppendLine(")");
        sb.AppendLine("    {");
        for (int i = 0; i < byType.Count; i++)
        {
            sb.AppendLine($"        _handler{i} = handler{i};");
        }
        sb.AppendLine("    }");
        sb.AppendLine();
        for (int i = 0; i < byType.Count; i++)
        {
            sb.AppendLine($"    private readonly {byType[i].Key} _handler{i};");
        }
        sb.AppendLine();

        for (int i = 0; i < byType.Count; i++)
        {
            foreach (var ep in byType[i].OrderBy(e => e.DisplayName, System.StringComparer.Ordinal))
            {
                EmitFunctionsMethod(sb, ep, $"_handler{i}");
            }
        }

        // Static helpers.
        sb.AppendLine("    private static async Task<IActionResult> ExecuteAsync(HttpContext context, IResult result)");
        sb.AppendLine("    {");
        sb.AppendLine("        await result.ExecuteAsync(context);");
        sb.AppendLine("        return new EmptyResult();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static string? ReadQueryString(HttpRequest request, string name)");
        sb.AppendLine("        => request.Query.TryGetValue(name, out var v) && v.Count > 0 ? v.ToString() : null;");
        sb.AppendLine();
        sb.AppendLine("    private static int? ReadQueryInt(HttpRequest request, string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var s = ReadQueryString(request, name);");
        sb.AppendLine("        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static long? ReadQueryLong(HttpRequest request, string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var s = ReadQueryString(request, name);");
        sb.AppendLine("        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool? ReadQueryBool(HttpRequest request, string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        var s = ReadQueryString(request, name);");
        sb.AppendLine("        return bool.TryParse(s, out var b) ? b : null;");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        spc.AddSource("FellowshipApiFunctions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static readonly Regex RouteTokenRegex = new(@"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::[^}]+)?\??\}", RegexOptions.Compiled);

    private static void EmitFunctionsMethod(StringBuilder sb, EndpointInfo ep, string handlerField)
    {
        var routeTokens = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Match m in RouteTokenRegex.Matches(ep.Route))
        {
            routeTokens.Add(m.Groups["name"].Value);
        }

        // Parameters declared on the generated [Function] method:
        //   - HttpRequest request (always, holds [HttpTrigger])
        //   - any handler parameter whose name matches a route token (Functions binds it)
        //   - CancellationToken (passed through if handler uses it)
        var functionParams = new List<string>
        {
            $"[HttpTrigger(AuthorizationLevel.Anonymous, \"{ep.HttpMethod.ToLowerInvariant()}\", Route = \"{EscapeForString(ep.Route)}\")] HttpRequest request",
        };

        var queryReads = new List<string>();
        var callArgs = new List<string>();
        bool needsCancellationToken = false;

        foreach (var p in ep.Parameters)
        {
            if (p.Kind == ParamKind.HttpContext)
            {
                callArgs.Add("request.HttpContext");
            }
            else if (p.Kind == ParamKind.HttpRequest)
            {
                callArgs.Add("request");
            }
            else if (p.Kind == ParamKind.CancellationToken)
            {
                needsCancellationToken = true;
                callArgs.Add("cancellationToken");
            }
            else if (routeTokens.Contains(p.Name))
            {
                // Functions binds route values as method parameters.
                functionParams.Add($"{p.TypeFullName} {p.Name}");
                callArgs.Add(p.Name);
            }
            else
            {
                // Read from query string.
                var local = "__q_" + p.Name;
                queryReads.Add($"        var {local} = {QueryReaderFor(p)};");
                callArgs.Add(local);
            }
        }

        if (needsCancellationToken)
        {
            functionParams.Add("CancellationToken cancellationToken");
        }

        sb.AppendLine($"    [Function(\"{ep.DisplayName}\")]");
        sb.Append("    public async Task<IActionResult> ").Append(ep.DisplayName).Append('(').AppendLine();
        for (int i = 0; i < functionParams.Count; i++)
        {
            sb.Append("        ").Append(functionParams[i]);
            if (i < functionParams.Count - 1) sb.Append(',');
            sb.AppendLine();
        }
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        foreach (var read in queryReads)
        {
            sb.AppendLine(read);
        }
        sb.Append("        var __result = await ").Append(handlerField).Append('.').Append(ep.MethodName).Append('(')
            .Append(string.Join(", ", callArgs)).AppendLine(");");
        sb.AppendLine("        return await ExecuteAsync(request.HttpContext, __result);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string QueryReaderFor(ParameterInfo p)
    {
        // Strip nullable annotation for matching.
        var t = p.TypeFullName.TrimEnd('?').Trim();
        return t switch
        {
            "string" or "System.String" => $"ReadQueryString(request, \"{p.Name}\")",
            "int" or "System.Int32" => $"ReadQueryInt(request, \"{p.Name}\")",
            "long" or "System.Int64" => $"ReadQueryLong(request, \"{p.Name}\")",
            "bool" or "System.Boolean" => $"ReadQueryBool(request, \"{p.Name}\")",
            _ => $"ReadQueryString(request, \"{p.Name}\") /* unsupported type {p.TypeFullName} */",
        };
    }

    private static string EscapeForString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ---------- Models ----------

    private enum ParamKind { Value, HttpContext, HttpRequest, CancellationToken }

    private sealed record ParameterInfo(string Name, string TypeFullName, ParamKind Kind);

    private sealed record EndpointInfo(
        string MethodName,
        string DisplayName,
        string ContainingTypeFullName,
        string HttpMethod,
        string Route,
        ImmutableArray<ParameterInfo> Parameters)
    {
        public static EndpointInfo From(IMethodSymbol method, INamedTypeSymbol containingType, string httpMethod, string route)
        {
            var parameters = method.Parameters.Select(p =>
            {
                var typeFullName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                    .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
                        | SymbolDisplayMiscellaneousOptions.UseSpecialTypes));

                var kind = ClassifyParam(p.Type);
                return new ParameterInfo(p.Name, typeFullName, kind);
            }).ToImmutableArray();

            return new EndpointInfo(
                MethodName: method.Name,
                DisplayName: TrimAsync(method.Name),
                ContainingTypeFullName: containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                HttpMethod: httpMethod,
                Route: route,
                Parameters: parameters);
        }

        private static string TrimAsync(string name) =>
            name.EndsWith("Async", System.StringComparison.Ordinal) && name.Length > "Async".Length
                ? name.Substring(0, name.Length - "Async".Length)
                : name;

        private static ParamKind ClassifyParam(ITypeSymbol type)
        {
            var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return fullName switch
            {
                "global::Microsoft.AspNetCore.Http.HttpContext" => ParamKind.HttpContext,
                "global::Microsoft.AspNetCore.Http.HttpRequest" => ParamKind.HttpRequest,
                "global::System.Threading.CancellationToken" => ParamKind.CancellationToken,
                _ => ParamKind.Value,
            };
        }
    }
}
