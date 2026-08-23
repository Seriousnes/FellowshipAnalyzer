using System.Collections.Immutable;

using FellowshipAnalyzer.Generators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// FA0011: an <c>[On&lt;TEvent&gt;]</c> handler takes either no parameter, when the fact that the
/// event happened is the whole signal, or a single parameter assignable from <c>TEvent</c>
/// (concrete event, base class, interface, or a <c>OneOf&lt;…&gt;</c> with a uniquely-resolvable
/// slot for <c>TEvent</c>), and returns <c>void</c>, <c>Task</c>, or <c>ValueTask</c>. Without
/// this the source generator silently produces no subscription, leading to a confusing
/// "the handler never fires" debugging session.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OnHandlerSignatureAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FA0011";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "[On<TEvent>] handler signature mismatch",
        messageFormat: "Handler '{0}' marked with [On<{1}>] is incompatible: {2}",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source-generated subscription wires a delegate that invokes the handler with the dispatched event cast to the handler parameter type, or with no argument when the handler declares no parameter. A declared parameter type must be the event type, one of its base classes or interfaces, or a OneOf<…> with a uniquely-resolvable slot for the event type.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Ordinary) return;

        foreach (var attr in method.GetAttributes())
        {
            if (!IsOnAttribute(attr.AttributeClass)) continue;
            if (attr.AttributeClass!.TypeArguments.Length != 1) continue;
            if (attr.AttributeClass.TypeArguments[0] is not INamedTypeSymbol eventType) continue;

            var reason = DescribeProblem(method, eventType);
            if (reason is null) continue;

            var loc = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
            context.ReportDiagnostic(Diagnostic.Create(Rule, loc, method.Name, eventType.Name, reason));
        }
    }

    private static bool IsOnAttribute(INamedTypeSymbol? cls) =>
        cls is { Name: "OnAttribute" }
        && cls.ContainingNamespace?.ToDisplayString() == "FellowshipAnalyzer.Core.Analysis";

    private static string? DescribeProblem(IMethodSymbol method, INamedTypeSymbol eventType)
    {
        if (method.Parameters.Length > 1)
            return "handler must take no parameter or exactly one";

        if (HandlerSignatureRules.ClassifyReturn(method) is HandlerReturnKind.Unsupported)
            return "handler must return void, Task, or ValueTask";

        if (method.Parameters.Length == 0)
            return null;

        var paramType = method.Parameters[0].Type;
        if (paramType is not INamedTypeSymbol paramNamed)
            return "parameter type '" + paramType.ToDisplayString() + "' is not a named type";

        if (HandlerSignatureRules.IsOneOfParam(paramNamed, out var oneOf))
        {
            if (HandlerSignatureRules.TryResolveOneOfSlot(oneOf, eventType, out _, out var ambiguityReason))
                return null;
            return "parameter '" + paramNamed.ToDisplayString() + "' has no unique slot for "
                + eventType.Name + " (" + ambiguityReason + ")";
        }

        if (HandlerSignatureRules.IsCompatibleParam(paramNamed, eventType))
            return null;

        return "parameter type '" + paramNamed.ToDisplayString()
            + "' is not assignable from " + eventType.Name;
    }
}
