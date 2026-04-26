using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoStateStore_is_updated_concurrently;

public class Then_state_is_consistent
{
    [Fact]
    public async Task Then_state_is_consistent_under_concurrent_writes()
    {
        var sut = new RepoStateStore();

        await Parallel.ForEachAsync(Enumerable.Range(0, 20), async (i, ct) =>
        {
            await Task.Yield();
            var repos = new List<RepoInfo> { new RepoInfo { Name = $"Repo{i}", Path = $"/r{i}", Group = "Other" } };
            sut.SetRepos(repos);
        });

        sut.Repos.ShouldNotBeNull();
        sut.Repos.Count.ShouldBe(1);
    }
}
