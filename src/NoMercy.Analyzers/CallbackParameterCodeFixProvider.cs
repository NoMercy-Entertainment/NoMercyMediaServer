using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpSyntaxRewriter = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxRewriter;
using SyntaxFactory = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NoMercy.Analyzers;

[ExportCodeFixProvider(
    LanguageNames.CSharp,
    Name = nameof(CallbackParameterCodeFixProvider)
)]
[Shared]
public sealed class CallbackParameterCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticIds.CallbackParameter);

    // WellKnownFixAllProviders.BatchFixer computes each diagnostic's fix as an
    // independently edited copy of the document, then merges the copies via a
    // text diff. Files with several near-identical callback lambdas (common
    // here — the whole point of this analyzer is repeated single-letter
    // parameters) can fool that diff and splice in fabricated text. Build every
    // rename as one structural SyntaxNode.ReplaceNodes edit against a single
    // unmodified tree instead — no per-diagnostic documents to merge.
    public override FixAllProvider GetFixAllProvider() =>
        FixAllProvider.Create(FixAllInDocumentAsync);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode? root = await context
            .Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (root == null)
            return;

        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);

        // We must find the ParameterSyntax
        ParameterSyntax? parameter = node as ParameterSyntax;
        if (parameter == null)
            return;

        context.RegisterCodeFix(
            Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                Resources.CallbackParameterTitle,
                ct =>
                    RenameParameterAsync(
                        context.Document,
                        parameter,
                        ct
                    ),
                nameof(CallbackParameterCodeFixProvider)
            ),
            diagnostic
        );
    }

    private static async Task<Document> RenameParameterAsync(
        Document document,
        ParameterSyntax parameter,
        CancellationToken cancellationToken
    )
    {
        SyntaxNode? root = await document
            .GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);
        if (root == null)
            return document;

        // Find the lambda: a simple lambda's parameter is a direct child of the
        // lambda node, while a parenthesized lambda's parameter sits inside a
        // ParameterListSyntax — walk ancestors instead of assuming a fixed depth.
        LambdaExpressionSyntax? lambda = parameter.FirstAncestorOrSelf<LambdaExpressionSyntax>();
        if (lambda == null)
            return document;

        SemanticModel? model = await document
            .GetSemanticModelAsync(cancellationToken)
            .ConfigureAwait(false);
        string newName = ComputeNewName(model, parameter, lambda);

        Dictionary<string, string> renameMap = new() { [parameter.Identifier.Text] = newName };
        SyntaxNode newLambda = new LambdaRenameRewriter(renameMap).Visit(lambda);

        SyntaxNode? newRoot = root.ReplaceNode(lambda, newLambda);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document?> FixAllInDocumentAsync(
        FixAllContext fixAllContext,
        Document document,
        ImmutableArray<Diagnostic> diagnostics
    )
    {
        if (diagnostics.IsEmpty)
            return document;

        SyntaxNode? root = await document
            .GetSyntaxRootAsync(fixAllContext.CancellationToken)
            .ConfigureAwait(false);
        if (root is null)
            return document;

        SemanticModel? model = await document
            .GetSemanticModelAsync(fixAllContext.CancellationToken)
            .ConfigureAwait(false);

        // Group by enclosing lambda first: a parenthesized lambda can report one
        // diagnostic per single-letter parameter, and all of them must be renamed
        // together in a single rewrite of that lambda.
        Dictionary<LambdaExpressionSyntax, Dictionary<string, string>> renamesByLambda = new();

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (
                root.FindNode(diagnostic.Location.SourceSpan) is not ParameterSyntax parameter
            )
                continue;

            LambdaExpressionSyntax? lambda =
                parameter.FirstAncestorOrSelf<LambdaExpressionSyntax>();
            if (lambda is null)
                continue;

            if (
                !renamesByLambda.TryGetValue(
                    lambda,
                    out Dictionary<string, string>? renameMap
                )
            )
            {
                renameMap = new Dictionary<string, string>();
                renamesByLambda[lambda] = renameMap;
            }

            renameMap[parameter.Identifier.Text] = ComputeNewName(
                model,
                parameter,
                lambda
            );
        }

        if (renamesByLambda.Count == 0)
            return document;

        Dictionary<LambdaExpressionSyntax, LambdaExpressionSyntax> replacements = new();
        foreach (
            KeyValuePair<
                LambdaExpressionSyntax,
                Dictionary<string, string>
            > entry in renamesByLambda
        )
        {
            SyntaxNode rewritten = new LambdaRenameRewriter(entry.Value).Visit(
                entry.Key
            );
            replacements[entry.Key] = (LambdaExpressionSyntax)rewritten;
        }

        SyntaxNode newRoot = root.ReplaceNodes(
            replacements.Keys,
            (original, _) => replacements[original]
        );

        return document.WithSyntaxRoot(newRoot);
    }

    private static string ComputeNewName(
        SemanticModel? model,
        ParameterSyntax parameter,
        LambdaExpressionSyntax lambda
    )
    {
        string newName = parameter.Identifier.Text;

        if (lambda.Parent is ArgumentSyntax arg)
        {
            INamedTypeSymbol? type =
                model?.GetTypeInfo(arg.Expression).ConvertedType as INamedTypeSymbol;
            type = UnwrapExpressionType(type);
            IMethodSymbol? invoke = type?.DelegateInvokeMethod;

            int parameterIndex = lambda switch
            {
                ParenthesizedLambdaExpressionSyntax paren => paren.ParameterList.Parameters.IndexOf(
                    parameter
                ),
                _ => 0,
            };

            if (
                invoke is not null
                && parameterIndex >= 0
                && parameterIndex < invoke.Parameters.Length
            )
                newName = ToCamelCase(invoke.Parameters[parameterIndex].Type.Name);
        }

        // Fallback: if no delegate type, use a safe default
        if (newName == parameter.Identifier.Text)
            newName = "value";

        return EscapeIdentifier(newName);
    }

    private static INamedTypeSymbol? UnwrapExpressionType(INamedTypeSymbol? type)
    {
        // IQueryable-based LINQ (EF Core, etc.) takes Expression<TDelegate> as the
        // parameter type instead of TDelegate directly — unwrap it so the delegate's
        // Invoke method (and therefore the real element type) is still reachable.
        if (
            type is { TypeArguments.Length: 1 }
            && type.OriginalDefinition.ToDisplayString()
                == "System.Linq.Expressions.Expression<TDelegate>"
            && type.TypeArguments[0] is INamedTypeSymbol innerDelegate
        )
            return innerDelegate;

        return type;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    // A camelCased type name can collide with a reserved keyword (e.g. "String" ->
    // "string"), which isn't a legal bare identifier — escape it like the sibling
    // NamedArgumentsCodeFixProvider does for the same problem.
    private static string EscapeIdentifier(string identifier) =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(identifier)
        == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? identifier
            : $"@{identifier}";

    private sealed class LambdaRenameRewriter : CSharpSyntaxRewriter
    {
        private readonly IReadOnlyDictionary<string, string> _renameMap;

        public LambdaRenameRewriter(IReadOnlyDictionary<string, string> renameMap)
        {
            _renameMap = renameMap;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_renameMap.TryGetValue(node.Identifier.Text, out string? newName))
                return node.WithIdentifier(SyntaxFactory.Identifier(newName));

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitParameter(ParameterSyntax node)
        {
            if (_renameMap.TryGetValue(node.Identifier.Text, out string? newName))
                return node.WithIdentifier(SyntaxFactory.Identifier(newName));

            return base.VisitParameter(node);
        }
    }
}
