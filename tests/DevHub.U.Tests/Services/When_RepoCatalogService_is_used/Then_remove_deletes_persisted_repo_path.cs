using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_remove_deletes_persisted_repo_path(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new EfRepoCatalogService(db.Factory, NullLogger<EfRepoCatalogService>.Instance);
        using var repo = new TempGitRepo();

        await sut.AddAsync(repo.Path);
        await sut.RemoveAsync(repo.Path);
        var repoPaths = await sut.GetRepoPathsAsync();

        repoPaths.ShouldBeEmpty();
    }
}
