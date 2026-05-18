using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// FA0010: Module constructors must not accept <c>CombatLogParser</c> or <c>EventEmitter</c>.
/// Both are set on the module by the parser after DI resolution; accepting them in the ctor
/// would create circular initialization order. Use ctor injection for sibling modules, or
/// fall through to <c>Owner.GetModule&lt;T&gt;()</c> for last-resort cycle escapes (rung d in §3).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModuleConstructorAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FA0010";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Module constructor must not accept CombatLogParser or EventEmitter",
        messageFormat: "Module '{0}' must not accept '{1}' in its constructor; both are wired by the parser after DI resolution",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Modules are scoped DI services whose Owner and EventEmitter are assigned by the parser after construction. Accepting them in a ctor parameter creates a circular initialization order.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (typeSymbol.IsAbstract)
            return;

        if (!InheritsFromByName(typeSymbol, "Module"))
            return;

        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            foreach (var param in ctor.Parameters)
            {
                if (param.Type is not INamedTypeSymbol paramType)
                    continue;

                var name = paramType.Name;
                if (name is "CombatLogParser" or "EventEmitter")
                {
                    var location = param.Locations.Length > 0 ? param.Locations[0] : Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, typeSymbol.Name, name));
                }
            }
        }
    }

    private static bool InheritsFromByName(INamedTypeSymbol type, string baseTypeName)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.Name == baseTypeName)
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
