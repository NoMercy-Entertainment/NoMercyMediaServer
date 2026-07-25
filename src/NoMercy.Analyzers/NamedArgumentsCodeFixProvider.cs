using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NoMercy.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NamedArgumentsCodeFixProvider))]
[Shared]
public sealed class NamedArgumentsCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticDescriptors.RequireNamedArgumentsForComplexCalls.Id);

    // WellKnownFixAllProviders.BatchFixer computes each diagnostic's fix as an
    // independently-edited copy of the document, then merges the copies via a
    // text diff. Files with several near-identical argument lists (common here,
    // since the whole point of this analyzer is repeated multi-parameter calls)
    // can fool that diff and splice in fabricated text. FixAllProvider.Create
    // instead hands us every diagnostic for a document at once, so all fixes are
    // applied as one structural SyntaxNode.ReplaceNodes edit against a single
    // unmodified tree — no per-diagnostic documents to merge, nothing to corrupt.
    public override FixAllProvider GetFixAllProvider() =>
        FixAllProvider.Create(FixAllInDocumentAsync);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];

        context.RegisterCodeFix(
            Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                Resources.RequireNamedTitle,
                ct => FixDocumentAsync(context.Document, ImmutableArray.Create(diagnostic), ct),
                nameof(NamedArgumentsCodeFixProvider)
            ),
            diagnostic
        );
    }

    private static async Task<Document?> FixAllInDocumentAsync(
        FixAllContext fixAllContext,
        Document document,
        ImmutableArray<Diagnostic> diagnostics
    ) =>
        await FixDocumentAsync(document, diagnostics, fixAllContext.CancellationToken)
            .ConfigureAwait(false);

    private static async Task<Document> FixDocumentAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken ct
    )
    {
        if (diagnostics.IsEmpty)
            return document;

        SyntaxNode? root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        SemanticModel? semanticModel = await document
            .GetSemanticModelAsync(ct)
            .ConfigureAwait(false);
        if (semanticModel is null)
            return document;

        Dictionary<ArgumentSyntax, ArgumentSyntax> replacements = new();

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ArgumentSyntax argument)
                continue;

            if (argument.Parent?.Parent is not InvocationExpressionSyntax invocation)
                continue;

            // Resolve from the invocation itself, not invocation.Expression: for a
            // delegate-typed field/property call (e.g. EF.CompileQuery's Func<...>),
            // the expression alone binds to the field, while the invocation binds to
            // the delegate's Invoke method — which is what the analyzer diagnosed against.
            if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol symbol)
                continue;

            int index = invocation.ArgumentList.Arguments.IndexOf(argument);
            if (index < 0 || index >= symbol.Parameters.Length)
                continue;

            IParameterSymbol parameter = symbol.Parameters[index];
            IdentifierNameSyntax nameSyntax = SyntaxFactory.IdentifierName(
                EscapeIdentifier(parameter.Name)
            );

            ArgumentSyntax newArgument = argument
                .WithNameColon(SyntaxFactory.NameColon(nameSyntax))
                .WithTriviaFrom(argument);

            replacements[argument] = newArgument;
        }

        if (replacements.Count == 0)
            return document;

        SyntaxNode newRoot = root.ReplaceNodes(
            replacements.Keys,
            (original, _) => replacements[original]
        );

        return document.WithSyntaxRoot(newRoot);
    }

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : $"@{identifier}";
}
