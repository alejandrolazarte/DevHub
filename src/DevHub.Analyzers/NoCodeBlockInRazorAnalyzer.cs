using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DevHub.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoCodeBlockInRazorAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DH001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use code-behind for Blazor components",
        messageFormat: "'{0}' has an @code block — move it to '{0}.cs'",
        category: "DevHub.Style",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Blazor components must use .razor.cs code-behind files instead of inline @code blocks.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        foreach (var file in context.Options.AdditionalFiles)
        {
            if (!file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = file.GetText(context.CancellationToken);
            if (text is null)
            {
                continue;
            }

            foreach (var line in text.Lines)
            {
                var lineText = line.ToString().TrimStart();
                if (!lineText.StartsWith("@code", StringComparison.Ordinal))
                {
                    continue;
                }

                var fileName = System.IO.Path.GetFileName(file.Path);
                var location = Location.Create(
                    file.Path,
                    line.Span,
                    text.Lines.GetLinePositionSpan(line.Span));

                context.ReportDiagnostic(Diagnostic.Create(Rule, location, fileName));
                break;
            }
        }
    }
}
