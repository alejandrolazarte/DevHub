using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_update_modifies_rule
{
    [Fact]
    public async Task Then_update_modifies_rule_Run()
    {
        var factory = TestDatabaseHelper.CreateInMemoryFactory();
        var sut = new GroupRuleService(factory);

        var created = await sut.CreateAsync(new GroupRule { Name = "Original", Color = "red", Prefixes = ["a_"] });
        created.Name = "Updated";
        created.Color = "green";

        var result = await sut.UpdateAsync(created);

        result.Name.ShouldBe("Updated");
        result.Color.ShouldBe("green");
    }
}
