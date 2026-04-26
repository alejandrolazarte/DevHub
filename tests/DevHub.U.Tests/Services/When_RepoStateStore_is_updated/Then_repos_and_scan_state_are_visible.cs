using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoStateStore_is_updated;

public class Then_repos_and_scan_state_are_visible
{
    [Fact]
    public void Then_state_reflects_repos_after_SetRepos()
    {
        var sut = new RepoStateStore();

        sut.SetScanning(true);
        sut.IsScanning.ShouldBeTrue();

        var repos = new List<RepoInfo> { new RepoInfo { Name = "repo1", Path = "C:\\repo1", Group = "Other" } };
        sut.SetRepos(repos);

        sut.IsScanning.ShouldBeFalse();
        sut.Repos.Count.ShouldBe(1);
        sut.LastScanCompleted.ShouldNotBe(default);
    }
}
