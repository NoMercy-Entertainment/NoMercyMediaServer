// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NoMercy.Analyzers;

/// <summary>
/// NMS001 — flags <c>System.IO.Path.Combine</c> calls in files that reference
/// <c>NoMercy.Storage.IStorage</c> or <c>IStorageDriver</c>. Drivers in the
/// <c>NoMercy.Storage.Drivers</c> namespace are exempt because they legitimately
/// operate against the OS filesystem.
/// </summary>
[DiagnosticAnalyzer(firstLanguage: LanguageNames.CSharp)]
[SuppressMessage(category: "MicrosoftCodeAnalysisCorrectness", checkId: "RS1038")]
public sealed class PathCombineAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "NMS001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use IStorage.CombinePath instead of System.IO.Path.Combine for storage paths",
        messageFormat: "Replace Path.Combine with storage.CombinePath. Path.Combine uses the OS separator which violates the IStorage path contract (Rule 2: forward-slash separators only).",
        category: "NoMercy.Storage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IStorage.CombinePath is driver-aware and always produces forward-slash-separated paths. Path.Combine on Windows produces backslash paths that violate the IStorage path contract. Use #pragma warning disable NMS001 to suppress this for paths that are not storage-bound."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(item: Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(action: AnalyzeInvocation, syntaxKinds: SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        SimpleNameSyntax? invokedName = AnalyzerSyntaxHelpers.GetInvokedName(invocation: invocation);
        if (invokedName is null)
        {
            return;
        }

        if (
            !string.Equals(
                a: invokedName.Identifier.Text,
                b: "Combine",
                comparisonType: StringComparison.Ordinal
            )
        )
        {
            return;
        }

        // Resolve the symbol to confirm it is System.IO.Path.Combine.
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(expression: invocation);
        ImmutableArray<ISymbol> candidates = symbolInfo.CandidateSymbols;
        ISymbol? symbol = symbolInfo.Symbol ?? (candidates.Length > 0 ? candidates[index: 0] : null);

        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType?.ToDisplayString() != "System.IO.Path")
        {
            return;
        }

        // Exempt the NoMercy.Storage.Drivers namespace — drivers legitimately
        // call Path.Combine against the real OS filesystem.
        INamedTypeSymbol? containingType = GetContainingType(node: context.Node, model: context.SemanticModel);
        if (containingType is not null)
        {
            string typeNamespace =
                containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (
                AnalyzerSyntaxHelpers.IsNamespaceOrDescendant(
                    candidate: typeNamespace,
                    target: "NoMercy.Storage.Drivers"
                )
            )
            {
                return;
            }
        }

        // Check whether the current file references IStorage or IStorageDriver.
        if (!FileReferencesStorage(context: context))
        {
            return;
        }

        context.ReportDiagnostic(diagnostic: Diagnostic.Create(descriptor: Rule, location: invocation.GetLocation()));
    }

    /// <summary>
    /// Returns the <see cref="INamedTypeSymbol"/> of the class/struct/record that
    /// directly contains the given syntax node.
    /// </summary>
    private static INamedTypeSymbol? GetContainingType(SyntaxNode node, SemanticModel model)
    {
        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is TypeDeclarationSyntax typeDecl)
            {
                return model.GetDeclaredSymbol(declarationSyntax: typeDecl) as INamedTypeSymbol;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Returns true when the syntax tree contains any reference to the
    /// <c>NoMercy.Storage</c> namespace or to the <c>IStorage</c> /
    /// <c>IStorageDriver</c> type names.
    /// </summary>
    private static bool FileReferencesStorage(SyntaxNodeAnalysisContext context)
    {
        SyntaxNode root = context.Node.SyntaxTree.GetRoot(cancellationToken: context.CancellationToken);

        foreach (SyntaxNode descendant in root.DescendantNodes())
        {
            if (descendant is UsingDirectiveSyntax usingDirective)
            {
                string usingName = usingDirective.Name?.ToString() ?? string.Empty;
                if (AnalyzerSyntaxHelpers.IsNamespaceOrDescendant(candidate: usingName, target: "NoMercy.Storage"))
                {
                    return true;
                }
            }

            // Catch qualified references like NoMercy.Storage.IStorage used without
            // an explicit using directive (e.g. global using or fully-qualified param type).
            if (descendant is IdentifierNameSyntax identifier)
            {
                string name = identifier.Identifier.Text;
                if (name == "IStorage" || name == "IStorageDriver" || name == "IStorageFactory")
                {
                    SymbolInfo info = context.SemanticModel.GetSymbolInfo(
                        expression: identifier,
                        cancellationToken: context.CancellationToken
                    );
                    ImmutableArray<ISymbol> symCandidates = info.CandidateSymbols;
                    ISymbol? sym =
                        info.Symbol ?? (symCandidates.Length > 0 ? symCandidates[index: 0] : null);
                    if (sym is not null)
                    {
                        string ns = sym.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                        if (AnalyzerSyntaxHelpers.IsNamespaceOrDescendant(candidate: ns, target: "NoMercy.Storage"))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
