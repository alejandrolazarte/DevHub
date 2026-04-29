using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_import_skips_already_imported_repos(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new EfRepoCatalogService(db.Factory, NullLogger<EfRepoCatalogService>.Instance);
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
