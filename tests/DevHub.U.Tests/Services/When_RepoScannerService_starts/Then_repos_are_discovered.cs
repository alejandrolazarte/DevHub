using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoScannerService_starts;

public class Then_repos_are_discovered
{
    [Fact]
    public async Task Then_repos_are_discovered_Run()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        using var repo1 = new TempGitRepoAt(Path.Combine(rootPath, "RepoA"));
        using var repo2 = new TempGitRepoAt(Path.Combine(rootPath, "RepoB"));
        Directory.CreateDirectory(Path.Combine(rootPath, "NotARepo"));

        var store = new RepoStateStore();
        var gitService = new GitCliService();
        var options = Options.Create(new DevHubOptions
        {
            RootPath = rootPath
        });

        var catalogMock = new Mock<IRepoCatalogService>();
        catalogMock.SetupSequence(x => x.GetRepoPathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([repo1.Path, repo2.Path]);
        catalogMock.Setup(x => x.ImportFromRootAsync(rootPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var groupRuleMock = new Mock<IGroupRuleService>();
        groupRuleMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new RepoScannerService(gitService, catalogMock.Object, store, groupRuleMock.Object, options,
            NullLogger<RepoScannerService>.Instance);

        await sut.TriggerScanAsync(CancellationToken.None);

        store.Repos.Count.ShouldBe(2);
        store.Repos.Select(r => r.Name).ShouldContain("RepoA");
        store.Repos.Select(r => r.Name).ShouldContain("RepoB");
        store.Repos.All(r => r.IsGitRepo).ShouldBeTrue();

        TempGitRepo.ForceDeleteDirectory(rootPath);
    }
}
