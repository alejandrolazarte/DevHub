using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoStateStore_is_updated;

public class Then_OnStateChanged_fires
{
    [Fact]
    public void Then_event_fires_after_SetRepos()
    {
        var sut = new RepoStateStore();
        var fired = false;
        sut.OnStateChanged += () => fired = true;

        var repos = new List<RepoInfo> { new RepoInfo { Name = "A", Path = "/a", Group = "Other" } };
        sut.SetRepos(repos);

        fired.ShouldBeTrue();
        sut.Repos.Count.ShouldBe(1);
    }
}
