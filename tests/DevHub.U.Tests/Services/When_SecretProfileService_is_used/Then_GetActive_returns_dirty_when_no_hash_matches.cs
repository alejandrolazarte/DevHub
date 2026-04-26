using System.Text;
using DevHub.Services.SecretProfiles;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_GetActive_returns_dirty_when_no_hash_matches
{
    [Fact]
    public async Task Then_GetActive_returns_dirty_when_no_hash_matches_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        var live = SutBuilder.LiveSecretsPath;
        var devPath = SutBuilder.ProfilePath("dev");

        fs.Setup(x => x.FileExists(live)).Returns(true);
        fs.Setup(x => x.DirectoryExists(SutBuilder.ProfilesDir)).Returns(true);
        fs.Setup(x => x.ReadAllBytesAsync(live, It.IsAny<CancellationToken>()))
          .ReturnsAsync(Encoding.UTF8.GetBytes("{\"edited\":true}"));
        fs.Setup(x => x.EnumerateFiles(SutBuilder.ProfilesDir, "*.json")).Returns([devPath]);
        fs.Setup(x => x.ReadAllBytesAsync(devPath, It.IsAny<CancellationToken>()))
          .ReturnsAsync(Encoding.UTF8.GetBytes("{\"env\":\"dev\"}"));

        var info = await sut.GetActiveProfileAsync(SutBuilder.SvcName, CancellationToken.None);

        info.MatchedProfileName.ShouldBeNull();
        info.IsDirty.ShouldBeTrue();
        info.LiveSecretsExists.ShouldBeTrue();
    }
}
