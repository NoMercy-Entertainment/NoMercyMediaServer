using Microsoft.CodeAnalysis;

namespace NoMercy.Analyzers;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor RequireNamedArgumentsForSingleLetterCallbacks = new(
        DiagnosticIds.RequireNamedArguments,
        Resources.RequireNamedArgumentsTitle,
        Resources.RequireNamedArgumentsMessageFormat,
        "Usage",
        DiagnosticSeverity.Warning,
        true,
        Resources.RequireNamedArgumentsDescription
    );

    public static readonly DiagnosticDescriptor CallbackParameterShouldBeRenamed = new(
        DiagnosticIds.CallbackParameter,
        Resources.CallbackParameterShouldBeRenamedTitle,
        Resources.CallbackParameterShouldBeRenamedMessageFormat,
        "Naming",
        DiagnosticSeverity.Warning,
        true,
        Resources.CallbackParameterShouldBeRenamedDescription
    );
}
