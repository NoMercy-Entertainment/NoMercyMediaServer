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
        DiagnosticDescriptors.RequireNamedArgumentsForSingleLetterCallbacks;

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

        for (int index = 0; index < arguments.Count; index++)
        {
            ArgumentSyntax argumentSyntax = arguments[index];

            if (argumentSyntax.NameColon != null)
                continue;

            // Only a callback whose parameter is a single letter is opaque enough to
            // need the label: `Count(a => ...)` says nothing about what `a` is for,
            // while `Count(predicate: a => ...)` does. Every other argument reads fine
            // positionally, and labelling them all is what made this rule unusable.
            if (!LambdaArgumentHelpers.HasSingleLetterParameter(argumentSyntax.Expression))
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
}
