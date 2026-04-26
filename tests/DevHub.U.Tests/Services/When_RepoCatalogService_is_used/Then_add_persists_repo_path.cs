using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_add_persists_repo_path
{
    [Fact]
    public async Task Then_add_persists_repo_path_Run()
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
        var sut = new EfRepoCatalogService(factory, NullLogger<EfRepoCatalogService>.Instance);

        using var repo = new TempGitRepo();

        await sut.AddAsync(repo.Path);
        var repoPaths = await sut.GetRepoPathsAsync();

        repoPaths.ShouldContain(Path.GetFullPath(repo.Path));
    }
}
