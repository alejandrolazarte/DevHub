using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_import_from_root_adds_git_repos_only
{
    [Fact]
    public async Task Then_import_from_root_adds_git_repos_only_Run()
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

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        using var repoA = new TempGitRepoAt(Path.Combine(tempRoot, "RepoA"));
        using var repoB = new TempGitRepoAt(Path.Combine(tempRoot, "RepoB"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "Notes"));

        try
        {
            var imported = await sut.ImportFromRootAsync(tempRoot);
            var repoPaths = await sut.GetRepoPathsAsync();

            imported.ShouldBe(2);
            repoPaths.Count.ShouldBe(2);
            repoPaths.ShouldContain(Path.GetFullPath(repoA.Path));
            repoPaths.ShouldContain(Path.GetFullPath(repoB.Path));
        }
        finally
        {
            TempGitRepo.ForceDeleteDirectory(tempRoot);
        }
    }
}
