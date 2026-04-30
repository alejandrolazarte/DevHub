using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using AutoFixture;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_getAll_returns_rules_in_order
{
    [Fact]
    public async Task Then_getAll_returns_rules_in_order_Run()
    {
        var factory = TestDatabaseHelper.CreateInMemoryFactory();
        var sut = new GroupRuleService(factory);

        var fixture = new AutoFixture.Fixture();
        var first = fixture.Build<GroupRule>().With(r => r.Name, "First").With(r => r.Color, "red").With(r => r.Prefixes, new List<string> { "a_" }).Create();
        var second = fixture.Build<GroupRule>().With(r => r.Name, "Second").With(r => r.Color, "blue").With(r => r.Prefixes, new List<string> { "b_" }).Create();

        await sut.CreateAsync(first);
        await sut.CreateAsync(second);

        var result = await sut.GetAllAsync();

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("First");
        result[1].Name.ShouldBe("Second");
    }
}
