using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NoMercy.Analyzers;

[DiagnosticAnalyzer(firstLanguage: LanguageNames.CSharp)]
[SuppressMessage(category: "MicrosoftCodeAnalysisCorrectness", checkId: "RS1038")]
public sealed class NamedArgumentsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DiagnosticDescriptors.RequireNamedArgumentsForMultiParameterMethods;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(item: Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            action: AnalyzeInvocation,
            syntaxKinds: SyntaxKind.InvocationExpression
        );
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return;

        if (
            context.SemanticModel.GetSymbolInfo(expression: invocation).Symbol
            is not IMethodSymbol symbol
        )
            return;

        if (symbol.Parameters.Length <= 2)
            return;

        // A trailing params parameter can't be named without either wrapping every
        // trailing value in an explicit collection (params only accepts a name in
        // normal form, i.e. the whole array) or forcing a syntax error (C# disallows
        // a positional argument, which the params tail must remain, after any named
        // argument). Naming isn't practical either way, so skip the whole invocation.
        if (symbol.Parameters[index: symbol.Parameters.Length - 1].IsParams)
            return;

        foreach (
            ArgumentSyntax argumentSyntax in invocation.ArgumentList.Arguments.Where(
                predicate: argumentSyntax => argumentSyntax.NameColon == null
            )
        )
        {
            if (argumentSyntax.NameColon != null)
                continue;

            Diagnostic diagnostic = Diagnostic.Create(
                descriptor: Rule,
                location: argumentSyntax.GetLocation()
            );

            context.ReportDiagnostic(diagnostic: diagnostic);
        }
    }
}
