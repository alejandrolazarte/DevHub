using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_reorder_changes_order
{
    [Fact]
    public async Task Then_reorder_changes_order_Run()
    {
        var factory = TestDatabaseHelper.CreateInMemoryFactory();
        var sut = new GroupRuleService(factory);

        var first = await sut.CreateAsync(new GroupRule { Name = "First", Color = "red", Prefixes = ["a_"] });
        var second = await sut.CreateAsync(new GroupRule { Name = "Second", Color = "blue", Prefixes = ["b_"] });

        await sut.ReorderAsync([second.Id, first.Id]);

        var result = await sut.GetAllAsync();
        result[0].Name.ShouldBe("Second");
        result[1].Name.ShouldBe("First");
    }
}
