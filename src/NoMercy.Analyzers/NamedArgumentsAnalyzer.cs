using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NoMercy.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1038")]
public sealed class NamedArgumentsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DiagnosticDescriptors.RequireNamedArgumentsForComplexCalls;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return;

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol symbol)
            return;

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;

        // Long argument lists are where positional order stops being readable.
        bool callIsComplex = arguments.Count > 3;

        for (int index = 0; index < arguments.Count; index++)
        {
            ArgumentSyntax argumentSyntax = arguments[index];

            if (argumentSyntax.NameColon != null)
                continue;

            // Short calls are fine positionally unless the value itself carries no
            // meaning at the call site: `Detect(episodeFingerprints, true)` says
            // nothing, `Detect(episodeFingerprints, fromTail: true)` does. Naming
            // anything beyond these two cases is what made this rule unusable.
            if (!callIsComplex && !IsOpaqueLiteral(argumentSyntax.Expression))
                continue;

            if (index >= symbol.Parameters.Length)
                continue;

            // A params argument only accepts a name in normal form (the whole array),
            // so the label can't be added to the value sitting in the params tail.
            if (symbol.Parameters[index].IsParams)
                continue;

            Diagnostic diagnostic = Diagnostic.Create(Rule, argumentSyntax.GetLocation());

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// A bare literal whose parameter the reader cannot infer from the call site.
    /// Numbers and strings usually carry their own meaning ("en", 1080); a naked
    /// <c>true</c>/<c>false</c>/<c>null</c> never does.
    /// </summary>
    private static bool IsOpaqueLiteral(ExpressionSyntax expression) =>
        expression.IsKind(SyntaxKind.TrueLiteralExpression)
        || expression.IsKind(SyntaxKind.FalseLiteralExpression)
        || expression.IsKind(SyntaxKind.NullLiteralExpression);
}
