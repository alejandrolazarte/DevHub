using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Shouldly;

namespace DevHub.U.Tests.Services.When_GitCliService_gets_last_commit_date;

public class Then_LastCommitDate_is_parsed
{
    [Fact]
    public async Task Then_LastCommitDate_is_parsed_Run()
    {
        using var repo = new TempGitRepo();
        repo.CreateFile("tracked.txt");
        repo.StageAndCommit("latest");

        var sut = new GitCliService();

        var result = await sut.GetLastCommitDateAsync(repo.Path);

        (DateTime.Now - result).Duration().ShouldBeLessThan(TimeSpan.FromMinutes(1));
    }
}
