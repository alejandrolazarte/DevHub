using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DevHub.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestNamingAnalyzer : DiagnosticAnalyzer
{
    public const string FileLocationId = "DH100";
    public const string FileNameId = "DH101";
    public const string MultipleTestsId = "DH102";

    private static readonly DiagnosticDescriptor FileLocationRule = new(
        id: FileLocationId,
        title: "Test files must live in a When_* folder",
        messageFormat: "Test file must be placed inside a folder named 'When_...'; found '{0}'",
        category: "Tests.Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor FileNameRule = new(
        id: FileNameId,
        title: "Test file name must start with Then_",
        messageFormat: "Test file name must start with 'Then_': '{0}'",
        category: "Tests.Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleTestsRule = new(
        id: MultipleTestsId,
        title: "One test method per file",
        messageFormat: "Test files must contain exactly one xUnit [Fact] method; found {0}",
        category: "Tests.Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(FileLocationRule, FileNameRule, MultipleTestsRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        if (root is null)
        {
            return;
        }

        var methodNodes = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        var factMethods = methodNodes.Where(m => HasFactAttribute(m)).ToList();
        if (factMethods.Count == 0)
        {
            return; // not a test file
        }

        var filePath = context.Tree.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        var fileName = Path.GetFileName(filePath);
        var parentDir = Path.GetFileName(Path.GetDirectoryName(filePath) ?? string.Empty) ?? string.Empty;

        // Rule: parent folder must start with When_
        if (!parentDir.StartsWith("When_", StringComparison.Ordinal))
        {
            var location = factMethods.First().Identifier.GetLocation();
            var diag = Diagnostic.Create(FileLocationRule, location, parentDir);
            context.ReportDiagnostic(diag);
        }

        // Rule: filename must start with Then_
        if (!fileName.StartsWith("Then_", StringComparison.OrdinalIgnoreCase))
        {
            var location = factMethods.First().Identifier.GetLocation();
            var diag = Diagnostic.Create(FileNameRule, location, fileName);
            context.ReportDiagnostic(diag);
        }

        // Rule: exactly one Fact method per file
        if (factMethods.Count != 1)
        {
            // report at the first fact method identifier location
            var location = factMethods.First().Identifier.GetLocation();
            var diag = Diagnostic.Create(MultipleTestsRule, location, factMethods.Count);
            context.ReportDiagnostic(diag);
        }
    }

    private static bool HasFactAttribute(MethodDeclarationSyntax method)
    {
        if (method.AttributeLists == null || method.AttributeLists.Count == 0)
        {
            return false;
        }

        foreach (var list in method.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                // matches Fact, FactAttribute, Xunit.Fact
                if (name.EndsWith("Fact", StringComparison.Ordinal) || name.EndsWith("FactAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
