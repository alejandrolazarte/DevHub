using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GitCliService_scans_clean_repo;

public class Then_IsDirty_is_false
{
    [Fact]
    public async Task Then_IsDirty_is_false_Run()
    {
        using var repo = new TempGitRepo();
        var sut = new GitCliService();

        var result = await sut.GetStatusAsync(repo.Path);

        result.IsDirty.ShouldBeFalse();
        result.DirtyFileCount.ShouldBe(0);
    }
}
