using System.Text;
using DevHub.Services.SecretProfiles;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_Delete_refuses_active_profile
{
    [Fact]
    public async Task Then_Delete_refuses_active_profile_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        var live = SutBuilder.LiveSecretsPath;
        var devPath = SutBuilder.ProfilePath("dev");
        var bytes = Encoding.UTF8.GetBytes("{\"env\":\"dev\"}");

        fs.Setup(x => x.FileExists(live)).Returns(true);
        fs.Setup(x => x.DirectoryExists(SutBuilder.ProfilesDir)).Returns(true);
        fs.Setup(x => x.ReadAllBytesAsync(live, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        fs.Setup(x => x.EnumerateFiles(SutBuilder.ProfilesDir, "*.json")).Returns([devPath]);
        fs.Setup(x => x.ReadAllBytesAsync(devPath, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);

        var act = () => sut.DeleteAsync(SutBuilder.SvcName, "dev", CancellationToken.None);

        await Should.ThrowAsync<CannotDeleteActiveProfileException>(act);
        fs.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }
}
