using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SymbolClassifier_classifies;

public class Then_ts_import_returns_UsesImport
{
    [Fact]
    public async Task Execute()
    {
        await Task.CompletedTask;
        var result = SymbolClassifier.Classify("app.tsx", "import { IOrderService } from './types'", "IOrderService");

        result.ShouldNotBeNull();
        result!.Value.Language.ShouldBe(SymbolLanguage.TypeScript);
        result!.Value.Role.ShouldBe(SymbolRole.UsesImport);
    }
}
