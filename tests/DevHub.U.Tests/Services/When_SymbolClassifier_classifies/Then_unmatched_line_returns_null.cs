using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SymbolClassifier_classifies;

public class Then_unmatched_line_returns_null
{
    [Fact]
    public async Task Execute()
    {
        await Task.CompletedTask;
        // A comment line containing the term — no role pattern matches
        var result = SymbolClassifier.Classify("Foo.cs", "// TODO: refactor IOrderService later", "IOrderService");

        result.ShouldBeNull();
    }
}
