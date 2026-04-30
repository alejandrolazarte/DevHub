using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SymbolClassifier_classifies;

public class Then_unmatched_line_returns_null
{
    [Fact]
    public async Task Execute()
    {
        await Task.CompletedTask;
        // Term appears at end of a comment with no following word — no pattern matches
        var result = SymbolClassifier.Classify("Foo.cs", "// This component depends on IOrderService", "IOrderService");

        result.ShouldBeNull();
    }
}
