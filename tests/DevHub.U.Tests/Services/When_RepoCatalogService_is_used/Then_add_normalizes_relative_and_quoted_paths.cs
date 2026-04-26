using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_add_normalizes_relative_and_quoted_paths
{
    [Fact]
    public async Task Then_add_normalizes_relative_and_quoted_paths_Run()
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
        using var repo = new TempGitRepoAt(Path.Combine(tempRoot, "RepoA"));

        var previousDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempRoot);
        try
        {
            var relativePath = $".{Path.DirectorySeparatorChar}RepoA";
            await sut.AddAsync($"\"{relativePath}\"");
            var repoPaths = await sut.GetRepoPathsAsync();

            repoPaths.Count.ShouldBe(1);
            repoPaths[0].ShouldBe(Path.GetFullPath(repo.Path));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            TempGitRepo.ForceDeleteDirectory(tempRoot);
        }
    }
}
