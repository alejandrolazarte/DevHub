using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_delete_removes_rule
{
    [Fact]
    public async Task Then_delete_removes_rule_Run()
    {
        var factory = TestDatabaseHelper.CreateInMemoryFactory();
        var sut = new GroupRuleService(factory);

        var created = await sut.CreateAsync(new GroupRule { Name = "ToDelete", Color = "red", Prefixes = ["a_"] });

        await sut.DeleteAsync(created.Id);

        var result = await sut.GetByIdAsync(created.Id);
        result.ShouldBeNull();
    }
}
