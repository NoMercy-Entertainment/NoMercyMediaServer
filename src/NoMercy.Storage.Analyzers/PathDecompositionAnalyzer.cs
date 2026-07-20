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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NoMercy.Storage.Analyzers;

/// <summary>
/// NMS002 — flags <c>System.IO.Path.GetDirectoryName</c>,
/// <c>System.IO.Path.GetFileName</c>, <c>System.IO.Path.GetFileNameWithoutExtension</c>,
/// and <c>System.IO.Path.GetFullPath</c> in files that reference
/// <c>NoMercy.Storage.IStorage</c> or <c>IStorageDriver</c>. These methods interpret
/// paths using OS conventions which break on storage-relative forward-slash paths on Windows.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PathDecompositionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "NMS002";

    private static readonly ImmutableHashSet<string> FlaggedMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetDirectoryName",
        "GetFileName",
        "GetFileNameWithoutExtension",
        "GetFullPath"
    );

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid System.IO.Path decomposition methods on storage-relative paths",
        messageFormat: "Path.{0} uses OS path conventions. On Windows this misinterprets forward-slash storage paths. Use string operations or expose a driver-aware equivalent via IStorage instead.",
        category: "NoMercy.Storage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Path.GetDirectoryName, GetFileName, GetFileNameWithoutExtension, and GetFullPath treat paths as OS-native, which on Windows means backslash delimiters. Storage-relative paths use forward slashes (IStorage Rule 2). Use #pragma warning disable NMS002 to suppress this for paths that are not storage-bound."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        SimpleNameSyntax? invokedName = AnalyzerSyntaxHelpers.GetInvokedName(invocation);
        if (invokedName is null)
        {
            return;
        }

        string methodName = invokedName.Identifier.Text;
        if (!FlaggedMethods.Contains(methodName))
        {
            return;
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        ImmutableArray<ISymbol> candidates = symbolInfo.CandidateSymbols;
        ISymbol? symbol = symbolInfo.Symbol ?? (candidates.Length > 0 ? candidates[0] : null);

        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType?.ToDisplayString() != "System.IO.Path")
        {
            return;
        }

        // Exempt driver implementations.
        INamedTypeSymbol? containingType = GetContainingType(context.Node, context.SemanticModel);
        if (containingType is not null)
        {
            string typeNamespace =
                containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (
                AnalyzerSyntaxHelpers.IsNamespaceOrDescendant(
                    typeNamespace,
                    "NoMercy.Storage.Drivers"
                )
            )
            {
                return;
            }
        }

        if (!FileReferencesStorage(context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }

    private static INamedTypeSymbol? GetContainingType(SyntaxNode node, SemanticModel model)
    {
        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is TypeDeclarationSyntax typeDecl)
            {
                return model.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool FileReferencesStorage(SyntaxNodeAnalysisContext context)
    {
        SyntaxNode root = context.Node.SyntaxTree.GetRoot(context.CancellationToken);

        foreach (SyntaxNode descendant in root.DescendantNodes())
        {
            if (descendant is UsingDirectiveSyntax usingDirective)
            {
                string usingName = usingDirective.Name?.ToString() ?? string.Empty;
                if (AnalyzerSyntaxHelpers.IsNamespaceOrDescendant(usingName, "NoMercy.Storage"))
                {
                    return true;
                }
            }

            if (descendant is IdentifierNameSyntax identifier)
            {
                string name = identifier.Identifier.Text;
                if (name == "IStorage" || name == "IStorageDriver" || name == "IStorageFactory")
                {
                    SymbolInfo info = context.SemanticModel.GetSymbolInfo(
                        identifier,
                        context.CancellationToken
                    );
                    ImmutableArray<ISymbol> symCandidates = info.CandidateSymbols;
                    ISymbol? sym =
                        info.Symbol ?? (symCandidates.Length > 0 ? symCandidates[0] : null);
                    if (sym is not null)
                    {
                        string ns = sym.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                        if (AnalyzerSyntaxHelpers.IsNamespaceOrDescendant(ns, "NoMercy.Storage"))
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
