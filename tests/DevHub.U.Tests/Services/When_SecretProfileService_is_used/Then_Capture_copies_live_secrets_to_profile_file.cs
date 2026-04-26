using System.Text;
using DevHub.Services.SecretProfiles;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_Capture_copies_live_secrets_to_profile_file
{
    [Fact]
    public async Task Then_Capture_copies_live_secrets_to_profile_file_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        var live = SutBuilder.LiveSecretsPath;
        var dest = SutBuilder.ProfilePath("dev");
        var bytes = Encoding.UTF8.GetBytes("{\"x\":1}");

        fs.Setup(x => x.FileExists(live)).Returns(true);
        fs.Setup(x => x.DirectoryExists(SutBuilder.ProfilesDir)).Returns(true);
        fs.Setup(x => x.ReadAllBytesAsync(live, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        fs.Setup(x => x.WriteAllBytesAsync(dest, bytes, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.CaptureAsync(SutBuilder.SvcName, "dev", CancellationToken.None);

        fs.Verify(x => x.WriteAllBytesAsync(dest, bytes, It.IsAny<CancellationToken>()), Times.Once);
    }
}
