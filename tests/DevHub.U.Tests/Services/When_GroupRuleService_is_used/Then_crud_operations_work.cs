using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_create_adds_rule_with_order(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new GroupRuleService(db.Factory);

        var result = await sut.CreateAsync(new GroupRule { Name = "Test", Color = "primary", Prefixes = ["fw_"] });

        result.Id.ShouldNotBe(0);
        result.Order.ShouldBe(0);
    }
}

public class Then_getAll_returns_rules_in_order(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new GroupRuleService(db.Factory);

        await sut.CreateAsync(new GroupRule { Name = "First", Color = "red", Prefixes = ["a_"] });
        await sut.CreateAsync(new GroupRule { Name = "Second", Color = "blue", Prefixes = ["b_"] });

        var result = await sut.GetAllAsync();

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("First");
        result[1].Name.ShouldBe("Second");
    }
}

public class Then_update_modifies_rule(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new GroupRuleService(db.Factory);

        var created = await sut.CreateAsync(new GroupRule { Name = "Original", Color = "red", Prefixes = ["a_"] });
        created.Name = "Updated";
        created.Color = "green";

        var result = await sut.UpdateAsync(created);

        result.Name.ShouldBe("Updated");
        result.Color.ShouldBe("green");
    }
}

public class Then_delete_removes_rule(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new GroupRuleService(db.Factory);

        var created = await sut.CreateAsync(new GroupRule { Name = "ToDelete", Color = "red", Prefixes = ["a_"] });

        await sut.DeleteAsync(created.Id);

        var result = await sut.GetByIdAsync(created.Id);
        result.ShouldBeNull();
    }
}

public class Then_reorder_changes_order(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new GroupRuleService(db.Factory);

        var first = await sut.CreateAsync(new GroupRule { Name = "First", Color = "red", Prefixes = ["a_"] });
        var second = await sut.CreateAsync(new GroupRule { Name = "Second", Color = "blue", Prefixes = ["b_"] });

        await sut.ReorderAsync([second.Id, first.Id]);

        var result = await sut.GetAllAsync();
        result[0].Name.ShouldBe("Second");
        result[1].Name.ShouldBe("First");
    }
}
