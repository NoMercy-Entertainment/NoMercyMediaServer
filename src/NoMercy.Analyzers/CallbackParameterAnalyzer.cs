using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NoMercy.Analyzers;

[DiagnosticAnalyzer(firstLanguage: LanguageNames.CSharp)]
[SuppressMessage(
    category: "MicrosoftCodeAnalysisCorrectness",
    checkId: "RS1038:Compiler extensions should be implemented in assemblies with compiler-provided references"
)]
public sealed class CallbackParameterAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule =
        DiagnosticDescriptors.CallbackParameterShouldBeRenamed;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(item: Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            action: AnalyzeLambda,
            syntaxKinds: SyntaxKind.SimpleLambdaExpression
        );
        context.RegisterSyntaxNodeAction(
            action: AnalyzeLambda,
            syntaxKinds: SyntaxKind.ParenthesizedLambdaExpression
        );
    }

    private static void AnalyzeLambda(SyntaxNodeAnalysisContext ctx)
    {
        switch (ctx.Node)
        {
            case SimpleLambdaExpressionSyntax simple:
                AnalyzeSimpleLambda(ctx: ctx, lambda: simple);
                break;

            case ParenthesizedLambdaExpressionSyntax paren:
                AnalyzeParenLambda(ctx: ctx, lambda: paren);
                break;
        }
    }

    private static void AnalyzeSimpleLambda(
        SyntaxNodeAnalysisContext ctx,
        SimpleLambdaExpressionSyntax lambda
    )
    {
        if (!IsCallbackLambda(lambda: lambda))
            return;

        var p = lambda.Parameter;

        if (IsSingleLetter(name: p.Identifier.Text))
        {
            ctx.ReportDiagnostic(
                diagnostic: Diagnostic.Create(
                    descriptor: Rule,
                    location: p.GetLocation(),
                    messageArgs: p.Identifier.Text
                )
            );
        }
    }

    private static void AnalyzeParenLambda(
        SyntaxNodeAnalysisContext ctx,
        ParenthesizedLambdaExpressionSyntax lambda
    )
    {
        if (!IsCallbackLambda(lambda: lambda))
            return;

        foreach (var p in lambda.ParameterList.Parameters)
        {
            if (IsSingleLetter(name: p.Identifier.Text))
            {
                ctx.ReportDiagnostic(
                    diagnostic: Diagnostic.Create(
                        descriptor: Rule,
                        location: p.GetLocation(),
                        messageArgs: p.Identifier.Text
                    )
                );
            }
        }
    }

    private static bool IsSingleLetter(string name) => name.Length == 1 && name != "_";

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
