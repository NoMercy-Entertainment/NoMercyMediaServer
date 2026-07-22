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

[ExportCodeFixProvider(
    firstLanguage: LanguageNames.CSharp,
    Name = nameof(NamedArgumentsCodeFixProvider)
)]
[Shared]
public sealed class NamedArgumentsCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            item: DiagnosticDescriptors.RequireNamedArgumentsForMultiParameterMethods.Id
        );

    // WellKnownFixAllProviders.BatchFixer computes each diagnostic's fix as an
    // independently-edited copy of the document, then merges the copies via a
    // text diff. Files with several near-identical argument lists (common here,
    // since the whole point of this analyzer is repeated multi-parameter calls)
    // can fool that diff and splice in fabricated text. FixAllProvider.Create
    // instead hands us every diagnostic for a document at once, so all fixes are
    // applied as one structural SyntaxNode.ReplaceNodes edit against a single
    // unmodified tree — no per-diagnostic documents to merge, nothing to corrupt.
    public override FixAllProvider GetFixAllProvider() =>
        FixAllProvider.Create(fixAllAsync: FixAllInDocumentAsync);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[index: 0];

        context.RegisterCodeFix(
            action: Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                title: Resources.RequireNamedTitle,
                createChangedDocument: ct =>
                    FixDocumentAsync(
                        document: context.Document,
                        diagnostics: ImmutableArray.Create(item: diagnostic),
                        ct: ct
                    ),
                equivalenceKey: nameof(NamedArgumentsCodeFixProvider)
            ),
            diagnostic: diagnostic
        );
    }

    private static async Task<Document?> FixAllInDocumentAsync(
        FixAllContext fixAllContext,
        Document document,
        ImmutableArray<Diagnostic> diagnostics
    ) =>
        await FixDocumentAsync(
                document: document,
                diagnostics: diagnostics,
                ct: fixAllContext.CancellationToken
            )
            .ConfigureAwait(continueOnCapturedContext: false);

    private static async Task<Document> FixDocumentAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken ct
    )
    {
        if (diagnostics.IsEmpty)
            return document;

        SyntaxNode? root = await document
            .GetSyntaxRootAsync(cancellationToken: ct)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (root is null)
            return document;

        SemanticModel? semanticModel = await document
            .GetSemanticModelAsync(cancellationToken: ct)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (semanticModel is null)
            return document;

        Dictionary<ArgumentSyntax, ArgumentSyntax> replacements = new();

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (root.FindNode(span: diagnostic.Location.SourceSpan) is not ArgumentSyntax argument)
                continue;

            if (argument.Parent?.Parent is not InvocationExpressionSyntax invocation)
                continue;

            // Resolve from the invocation itself, not invocation.Expression: for a
            // delegate-typed field/property call (e.g. EF.CompileQuery's Func<...>),
            // the expression alone binds to the field, while the invocation binds to
            // the delegate's Invoke method — which is what the analyzer diagnosed against.
            if (
                semanticModel.GetSymbolInfo(expression: invocation, cancellationToken: ct).Symbol
                is not IMethodSymbol symbol
            )
                continue;

            int index = invocation.ArgumentList.Arguments.IndexOf(node: argument);
            if (index < 0 || index >= symbol.Parameters.Length)
                continue;

            IParameterSymbol parameter = symbol.Parameters[index: index];
            IdentifierNameSyntax nameSyntax = SyntaxFactory.IdentifierName(
                name: EscapeIdentifier(identifier: parameter.Name)
            );

            ArgumentSyntax newArgument = argument
                .WithNameColon(nameColon: SyntaxFactory.NameColon(name: nameSyntax))
                .WithTriviaFrom(node: argument);

            replacements[key: argument] = newArgument;
        }

        if (replacements.Count == 0)
            return document;

        SyntaxNode newRoot = root.ReplaceNodes(
            nodes: replacements.Keys,
            computeReplacementNode: (original, _) => replacements[key: original]
        );

        return document.WithSyntaxRoot(root: newRoot);
    }

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(text: identifier) == SyntaxKind.None
            ? identifier
            : $"@{identifier}";
}
