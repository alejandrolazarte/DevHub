using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SymbolClassifier_classifies;

public class Then_cs_class_definition_returns_DefineClass
{
    [Fact]
    public async Task Execute()
    {
        await Task.CompletedTask;
        var result = SymbolClassifier.Classify("OrderService.cs", "public class OrderService : IOrderService", "OrderService");

        result.ShouldNotBeNull();
        result!.Value.Language.ShouldBe(SymbolLanguage.CSharp);
        result!.Value.Role.ShouldBe(SymbolRole.DefineClass);
    }
}
