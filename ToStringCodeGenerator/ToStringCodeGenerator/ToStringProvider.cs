using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ToStringAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ToStringFixProvider)), Shared]
    public class ToStringFixProvider : CodeFixProvider
    {
        private const string title = "Generate ToString()";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ToStringAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.

            var action = CodeAction.Create(
                    title: title,
                    // createChangedSolution: c => GenerateToString(context.Document, declaration, c),
                    createChangedDocument: c => GenerateToString(context.Document, declaration, c),
                    equivalenceKey: title);

            context.RegisterCodeFix(action, diagnostic);
        }

        private async Task<Document> GenerateToString(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            var newClassDec = typeDecl.AddMembers(GetToStringDeclarationSyntax(typeDecl, semanticModel));

            var sr = await document.GetSyntaxRootAsync(cancellationToken);

            var nsr = sr.ReplaceNode(typeDecl, newClassDec);

            var newDocument = document.WithSyntaxRoot(nsr);

            return newDocument;
        }


        private MethodDeclarationSyntax GetToStringDeclarationSyntax(TypeDeclarationSyntax typeDeclarationSyntax, SemanticModel semanticModel)
        {
            return SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.List<AttributeListSyntax>(),
                            SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.OverrideKeyword)),
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            null,
                            SyntaxFactory.Identifier(ToStringAnalyzer.ToStringMethod),
                            null,
                            SyntaxFactory.ParameterList(),
                            SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
                            GetToStringBody(typeDeclarationSyntax, semanticModel),
                            null);
        }

        private BlockSyntax GetToStringBody(TypeDeclarationSyntax classDeclarationSyntax, SemanticModel semanticModel)
        {
            var sm = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);

            var properties = FindAllProperties(sm);

            var r = string.Join(", ", properties.Select(p => p.GetPrintedValueForCSharp()));

            var @return = SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression("$\"{{" + r + "}}\""));

            return SyntaxFactory.Block(@return);
        }

        public static IEnumerable<PropertyInfo> FindAllProperties(INamedTypeSymbol symbol)
        {
            var props = symbol.GetMembers().Where(p => p.Kind == SymbolKind.Property &&
            p.DeclaredAccessibility == Accessibility.Public).OfType<IPropertySymbol>();
            foreach (var item in props)
            {
                yield return new PropertyInfo(item.Name, item.Type.IsValueType);
            }

            if (!string.Equals(symbol.BaseType?.Name, "object", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in FindAllProperties(symbol.BaseType))
                {
                    yield return item;
                }
            }
        }
    }
}
