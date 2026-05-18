using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// FA0012: A class carrying <c>[HeroAnalyzer]</c> must be declared <c>partial</c>. The
/// CombatLogParserGenerator emits the constructor, module accessor properties, and the
/// DI extension into a partial alongside the user's declaration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HeroAnalyzerPartialAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FA0012";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Hero analyzer class must be partial",
        messageFormat: "Class '{0}' is marked with [HeroAnalyzer] but is not declared 'partial'",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The CombatLogParserGenerator emits the constructor, typed module properties, and the DI extension as a partial declaration alongside the user's class.");

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

        if (!typeSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "HeroAnalyzerAttribute"))
            return;

        // A class is partial when any of its declaring syntax references carries the partial modifier.
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is ClassDeclarationSyntax classDecl)
            {
                foreach (var modifier in classDecl.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.PartialKeyword))
                        return; // already partial — no diagnostic
                }
            }
        }

        var loc = typeSymbol.Locations.Length > 0 ? typeSymbol.Locations[0] : Location.None;
        context.ReportDiagnostic(Diagnostic.Create(Rule, loc, typeSymbol.Name));
    }
}
