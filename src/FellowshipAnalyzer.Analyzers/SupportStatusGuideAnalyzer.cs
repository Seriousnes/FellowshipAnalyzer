using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// FA0017: a hero whose <c>HeroConfig.Support</c> is <c>Partial</c> or <c>Full</c> must override
/// <c>GuideComponent</c>. A maintained status advertises analysis, so it must have analysis UI
/// behind it — otherwise the declared status and the hero's actual capability disagree.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SupportStatusGuideAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FA0017";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Maintained hero must have a guide",
        messageFormat: "Hero '{0}' declares Support = {1} but does not override GuideComponent; a maintained status requires a guide",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A hero config's Support level of Partial or Full advertises maintained analysis. Without a GuideComponent override the hero has no analysis UI, so declared status and actual capability disagree.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!type.GetAttributes().Any(a => a.AttributeClass?.Name == "HeroAnalyzerAttribute"))
            return;

        var configMember = type.GetMembers("HeroConfig").FirstOrDefault(m => m.IsStatic);
        if (configMember is null)
            return;

        var (level, location) = ReadMaintainedSupport(configMember, context.CancellationToken);
        if (level is null)
            return; // Unmaintained, unset, or not statically readable — nothing to check.

        if (OverridesGuideComponent(type))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, location ?? type.Locations[0], type.Name, level));
    }

    private static bool OverridesGuideComponent(INamedTypeSymbol type) =>
        type.GetMembers("GuideComponent").OfType<IPropertySymbol>().Any(p => p.IsOverride);

    /// <summary>
    /// Reads the <c>Support = SupportLevel.X</c> assignment from the config's object initializer.
    /// Returns the level name only when it is <c>Partial</c> or <c>Full</c>; otherwise null.
    /// </summary>
    private static (string? Level, Location? Location) ReadMaintainedSupport(ISymbol configMember, CancellationToken ct)
    {
        foreach (var syntaxRef in configMember.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax(ct);
            var initializer = node.DescendantNodesAndSelf()
                .OfType<InitializerExpressionSyntax>()
                .FirstOrDefault(i => i.IsKind(SyntaxKind.ObjectInitializerExpression));
            if (initializer is null)
                continue;

            foreach (var expr in initializer.Expressions)
            {
                if (expr is not AssignmentExpressionSyntax assign)
                    continue;
                if (assign.Left is not IdentifierNameSyntax id || id.Identifier.Text != "Support")
                    continue;

                var name = assign.Right switch
                {
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                    IdentifierNameSyntax i => i.Identifier.Text,
                    _ => null,
                };
                return name is "Partial" or "Full" ? (name, assign.GetLocation()) : (null, null);
            }
        }

        return (null, null);
    }
}
