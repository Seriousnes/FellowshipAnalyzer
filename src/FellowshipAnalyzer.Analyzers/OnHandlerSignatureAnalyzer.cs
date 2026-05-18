using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// FA0011: <c>[On&lt;TEvent&gt;]</c> handler signature must take a single parameter assignable from
/// <c>TEvent</c> and return <c>void</c>, <c>Task</c>, or <c>ValueTask</c>.
/// Without this the source generator silently produces no subscription, leading to a confusing
/// "the handler never fires" debugging session.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OnHandlerSignatureAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FA0011";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "[On<TEvent>] handler signature mismatch",
        messageFormat: "Handler '{0}' marked with [On<{1}>] must take a single parameter assignable from {1} and return void/Task/ValueTask",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source-generated subscription wires a delegate that invokes the handler with the dispatched event cast to the handler parameter type. The parameter type must be assignable from the [On<>] type argument.");

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

            var problem = ValidateSignature(method, eventType);
            if (problem)
            {
                var loc = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
                context.ReportDiagnostic(Diagnostic.Create(Rule, loc, method.Name, eventType.Name));
            }
        }
    }

    private static bool IsOnAttribute(INamedTypeSymbol? cls) =>
        cls is { Name: "OnAttribute" }
        && cls.ContainingNamespace?.ToDisplayString() == "FellowshipAnalyzer.Core.Analysis";

    private static bool ValidateSignature(IMethodSymbol method, INamedTypeSymbol eventType)
    {
        // Returns true when the signature is INVALID.
        if (method.Parameters.Length != 1) return true;

        var paramType = method.Parameters[0].Type;
        if (!IsAssignableFrom(paramType, eventType)) return true;

        if (method.ReturnsVoid) return false;
        var rt = method.ReturnType;
        if (rt.Name is "Task" or "ValueTask") return false;
        return true;
    }

    private static bool IsAssignableFrom(ITypeSymbol target, INamedTypeSymbol source)
    {
        if (SymbolEqualityComparer.Default.Equals(target, source)) return true;

        var current = source.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(target, current)) return true;
            current = current.BaseType;
        }
        foreach (var iface in source.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(target, iface)) return true;
        }
        return false;
    }
}
