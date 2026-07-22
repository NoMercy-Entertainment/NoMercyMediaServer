using Microsoft.CodeAnalysis;

namespace NoMercy.Analyzers;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor RequireNamedArgumentsForMultiParameterMethods = new(
        id: DiagnosticIds.RequireNamedArguments,
        title: Resources.RequireNamedArgumentsTitle,
        messageFormat: Resources.RequireNamedArgumentsMessageFormat,
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Resources.RequireNamedArgumentsDescription
    );

    public static readonly DiagnosticDescriptor CallbackParameterShouldBeRenamed = new(
        id: DiagnosticIds.CallbackParameter,
        title: Resources.CallbackParameterShouldBeRenamedTitle,
        messageFormat: Resources.CallbackParameterShouldBeRenamedMessageFormat,
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Resources.CallbackParameterShouldBeRenamedDescription
    );
}
