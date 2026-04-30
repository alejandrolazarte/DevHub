using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using AutoFixture;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_create_adds_rule_with_order
{
    [Fact]
    public async Task Then_create_adds_rule_with_order_Run()
    {
        var factory = TestDatabaseHelper.CreateInMemoryFactory();
        var sut = new GroupRuleService(factory);

        var fixture = new AutoFixture.Fixture();
        var rule = fixture.Build<GroupRule>().With(r => r.Name, "Test").With(r => r.Color, "primary").With(r => r.Prefixes, new List<string> { "fw_" }).Create();
        var result = await sut.CreateAsync(rule);

        result.Id.ShouldNotBe(0);
        result.Order.ShouldBe(0);
    }
}

