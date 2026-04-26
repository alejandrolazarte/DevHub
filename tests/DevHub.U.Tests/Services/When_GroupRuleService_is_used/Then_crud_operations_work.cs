using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GroupRuleService_is_used;

public class Then_create_adds_rule_with_order
{
    [Fact]
    public async Task Then_create_adds_rule_with_order_Run()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var factory = new TestDbContextFactory(options);
        var sut = new GroupRuleService(factory);

        var result = await sut.CreateAsync(new GroupRule { Name = "Test", Color = "primary", Prefixes = ["fw_"] });

        result.Id.ShouldNotBe(0);
        result.Order.ShouldBe(0);
    }
}

public class Then_getAll_returns_rules_in_order
{
    [Fact]
    public async Task Then_getAll_returns_rules_in_order_Run()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var factory = new TestDbContextFactory(options);
        var sut = new GroupRuleService(factory);

        await sut.CreateAsync(new GroupRule { Name = "First", Color = "red", Prefixes = ["a_"] });
        await sut.CreateAsync(new GroupRule { Name = "Second", Color = "blue", Prefixes = ["b_"] });

        var result = await sut.GetAllAsync();

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("First");
        result[1].Name.ShouldBe("Second");
    }
}

public class Then_update_modifies_rule
{
    [Fact]
    public async Task Then_update_modifies_rule_Run()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var factory = new TestDbContextFactory(options);
        var sut = new GroupRuleService(factory);

        var created = await sut.CreateAsync(new GroupRule { Name = "Original", Color = "red", Prefixes = ["a_"] });
        created.Name = "Updated";
        created.Color = "green";

        var result = await sut.UpdateAsync(created);

        result.Name.ShouldBe("Updated");
        result.Color.ShouldBe("green");
    }
}

public class Then_delete_removes_rule
{
    [Fact]
    public async Task Then_delete_removes_rule_Run()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var factory = new TestDbContextFactory(options);
        var sut = new GroupRuleService(factory);

        var created = await sut.CreateAsync(new GroupRule { Name = "ToDelete", Color = "red", Prefixes = ["a_"] });

        await sut.DeleteAsync(created.Id);

        var result = await sut.GetByIdAsync(created.Id);
        result.ShouldBeNull();
    }
}

public class Then_reorder_changes_order
{
    [Fact]
    public async Task Then_reorder_changes_order_Run()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var factory = new TestDbContextFactory(options);
        var sut = new GroupRuleService(factory);

        var first = await sut.CreateAsync(new GroupRule { Name = "First", Color = "red", Prefixes = ["a_"] });
        var second = await sut.CreateAsync(new GroupRule { Name = "Second", Color = "blue", Prefixes = ["b_"] });

        await sut.ReorderAsync([second.Id, first.Id]);

        var result = await sut.GetAllAsync();
        result[0].Name.ShouldBe("Second");
        result[1].Name.ShouldBe("First");
    }
}
