using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace FellowshipAnalyzer.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(JsonContextMissingSerializableCodeFix)), Shared]
public sealed class JsonContextMissingSerializableCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [JsonContextMissingSerializableAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        if (!diagnostic.Properties.TryGetValue(JsonContextMissingSerializableAnalyzer.MissingTypeSimpleNameKey, out var simpleName) ||
            string.IsNullOrEmpty(simpleName))
        {
            return;
        }

        diagnostic.Properties.TryGetValue(JsonContextMissingSerializableAnalyzer.MissingTypeNamespaceKey, out var ns);

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        // Find the class declaration at the diagnostic location.
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var classDecl = root.FindToken(diagnosticSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDecl is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add [JsonSerializable(typeof({simpleName}))]",
                createChangedDocument: ct => AddJsonSerializableAttributeAsync(
                    context.Document, root, classDecl, simpleName!, ns, ct),
                equivalenceKey: $"{JsonContextMissingSerializableAnalyzer.DiagnosticId}_{simpleName}"),
            diagnostic);
    }

    private static Task<Document> AddJsonSerializableAttributeAsync(
        Document document,
        SyntaxNode root,
        ClassDeclarationSyntax classDecl,
        string typeName,
        string? typeNamespace,
        CancellationToken _)
    {
        // Build: [JsonSerializable(typeof(TypeName))]
        var typeNameSyntax = string.IsNullOrEmpty(typeNamespace)
            ? (TypeSyntax)SyntaxFactory.IdentifierName(typeName)
            : SyntaxFactory.QualifiedName(
                SyntaxFactory.ParseName(typeNamespace!),
                SyntaxFactory.IdentifierName(typeName));

        var newAttrList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(
                    SyntaxFactory.IdentifierName("JsonSerializable"),
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.TypeOfExpression(typeNameSyntax)))))))
            .WithAdditionalAnnotations(Formatter.Annotation);

        // Insert after the last existing [JsonSerializable] attribute list (if any),
        // otherwise append to the end of all attribute lists.
        var attrLists = classDecl.AttributeLists;
        int insertIndex = attrLists.Count; // default: append at end

        for (int i = attrLists.Count - 1; i >= 0; i--)
        {
            var al = attrLists[i];
            if (al.Attributes.Any(a => a.Name.ToString().EndsWith("JsonSerializable", System.StringComparison.Ordinal)))
            {
                insertIndex = i + 1;
                break;
            }
        }

        var newAttrLists = attrLists.Insert(insertIndex, newAttrList);
        var newClassDecl = classDecl.WithAttributeLists(newAttrLists);
        var newRoot = root.ReplaceNode(classDecl, newClassDecl);

        return System.Threading.Tasks.Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
