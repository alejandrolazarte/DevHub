using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GitCliService_scans_dirty_repo;

public class Then_IsDirty_is_true
{
    [Fact]
    public async Task Then_IsDirty_is_true_Run()
    {
        using var repo = new TempGitRepo();
        repo.CreateFile("dirty.txt", "untracked content");
        var sut = new GitCliService();

        var result = await sut.GetStatusAsync(repo.Path);

        result.IsDirty.ShouldBeTrue();
        result.DirtyFileCount.ShouldBe(1);
    }
}
