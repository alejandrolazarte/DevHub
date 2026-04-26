using System.Text;
using DevHub.Services.SecretProfiles;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_GetActive_matches_profile_by_hash
{
    [Fact]
    public async Task Then_GetActive_matches_profile_by_hash_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        var live = SutBuilder.LiveSecretsPath;
        var devPath = SutBuilder.ProfilePath("dev");
        var prodPath = SutBuilder.ProfilePath("prod");
        var devBytes = Encoding.UTF8.GetBytes("{\"env\":\"dev\"}");
        var prodBytes = Encoding.UTF8.GetBytes("{\"env\":\"prod\"}");

        fs.Setup(x => x.FileExists(live)).Returns(true);
        fs.Setup(x => x.DirectoryExists(SutBuilder.ProfilesDir)).Returns(true);
        fs.Setup(x => x.ReadAllBytesAsync(live, It.IsAny<CancellationToken>())).ReturnsAsync(prodBytes);
        fs.Setup(x => x.EnumerateFiles(SutBuilder.ProfilesDir, "*.json")).Returns([devPath, prodPath]);
        fs.Setup(x => x.ReadAllBytesAsync(devPath, It.IsAny<CancellationToken>())).ReturnsAsync(devBytes);
        fs.Setup(x => x.ReadAllBytesAsync(prodPath, It.IsAny<CancellationToken>())).ReturnsAsync(prodBytes);

        var info = await sut.GetActiveProfileAsync(SutBuilder.SvcName, CancellationToken.None);

        info.MatchedProfileName.ShouldBe("prod");
        info.IsProd.ShouldBeTrue();
        info.IsDirty.ShouldBeFalse();
    }
}
