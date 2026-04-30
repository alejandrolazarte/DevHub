using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_remove_deletes_persisted_repo_path
{
    [Fact]
    public async Task Then_remove_deletes_persisted_repo_path_Run()
    {
        var factory = TestDatabaseHelper.CreateInMemoryFactory();
        var sut = new EfRepoCatalogService(factory, NullLogger<EfRepoCatalogService>.Instance);

        using var repo = new TempGitRepo();

        await sut.AddAsync(repo.Path);
        await sut.RemoveAsync(repo.Path);
        var repoPaths = await sut.GetRepoPathsAsync();

        repoPaths.ShouldBeEmpty();
    }
}
