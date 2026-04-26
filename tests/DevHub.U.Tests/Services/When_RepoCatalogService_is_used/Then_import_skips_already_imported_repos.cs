using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_import_skips_already_imported_repos
{
    [Fact]
    public async Task Execute()
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
        using var repo = new TempGitRepoAt(Path.Combine(tempRoot, "RepoA"));

        try
        {
            var firstImport = await sut.ImportFromRootAsync(tempRoot);
            var secondImport = await sut.ImportFromRootAsync(tempRoot);
            var repoPaths = await sut.GetRepoPathsAsync();

            firstImport.ShouldBe(1);
            secondImport.ShouldBe(0);
            repoPaths.Count.ShouldBe(1);
        }
        finally
        {
            TempGitRepo.ForceDeleteDirectory(tempRoot);
        }
    }
}
