using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NoMercy.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
[SuppressMessage(
    "MicrosoftCodeAnalysisCorrectness",
    "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references"
)]
public sealed class CallbackParameterAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule =
        DiagnosticDescriptors.CallbackParameterShouldBeRenamed;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.SimpleLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.ParenthesizedLambdaExpression);
    }

    private static void AnalyzeLambda(SyntaxNodeAnalysisContext ctx)
    {
        switch (ctx.Node)
        {
            case SimpleLambdaExpressionSyntax simple:
                AnalyzeSimpleLambda(ctx, simple);
                break;

            case ParenthesizedLambdaExpressionSyntax paren:
                AnalyzeParenLambda(ctx, paren);
                break;
        }
    }

    private static void AnalyzeSimpleLambda(
        SyntaxNodeAnalysisContext ctx,
        SimpleLambdaExpressionSyntax lambda
    )
    {
        if (!IsCallbackLambda(lambda))
            return;

        ParameterSyntax parameter = lambda.Parameter;

        if (LambdaArgumentHelpers.IsSingleLetter(parameter.Identifier.Text))
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(Rule, parameter.GetLocation(), parameter.Identifier.Text)
            );
        }
    }

    private static void AnalyzeParenLambda(
        SyntaxNodeAnalysisContext ctx,
        ParenthesizedLambdaExpressionSyntax lambda
    )
    {
        if (!IsCallbackLambda(lambda))
            return;

        foreach (ParameterSyntax parameter in lambda.ParameterList.Parameters)
        {
            if (LambdaArgumentHelpers.IsSingleLetter(parameter.Identifier.Text))
            {
                ctx.ReportDiagnostic(
                    Diagnostic.Create(Rule, parameter.GetLocation(), parameter.Identifier.Text)
                );
            }
        }
    }

    private static bool IsCallbackLambda(LambdaExpressionSyntax lambda)
    {
        // Must be inside an argument list
        if (lambda.Parent is not ArgumentSyntax arg)
            return false;

        // Must belong to an invocation. Whether the argument is named (e.g. by
        // NamedArgumentsAnalyzer's own fix) doesn't change whether the lambda is a
        // callback — the two analyzers must agree regardless of naming, or fixing
        // one diagnostic silently suppresses the other.
        return arg.Parent?.Parent is InvocationExpressionSyntax;
    }
}
