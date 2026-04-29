using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_add_normalizes_relative_and_quoted_paths(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new EfRepoCatalogService(db.Factory, NullLogger<EfRepoCatalogService>.Instance);
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
