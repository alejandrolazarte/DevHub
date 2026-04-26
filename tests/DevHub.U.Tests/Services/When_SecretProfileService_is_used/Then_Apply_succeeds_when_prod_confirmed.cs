using System.Text;
using DevHub.Services.SecretProfiles;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_Apply_succeeds_when_prod_confirmed
{
    [Fact]
    public async Task Then_Apply_succeeds_when_prod_confirmed_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        var profilePath = SutBuilder.ProfilePath("prod");
        var livePath = SutBuilder.LiveSecretsPath;
        var liveDir = Path.GetDirectoryName(livePath)!;
        var tmpPath = livePath + ".tmp";
        var bytes = Encoding.UTF8.GetBytes("{\"prod\":true}");

        fs.Setup(x => x.FileExists(profilePath)).Returns(true);
        fs.Setup(x => x.DirectoryExists(liveDir)).Returns(true);
        fs.Setup(x => x.ReadAllBytesAsync(profilePath, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        fs.Setup(x => x.WriteAllBytesAsync(tmpPath, bytes, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        fs.Setup(x => x.Move(tmpPath, livePath, true));

        await sut.ApplyAsync(SutBuilder.SvcName, "prod", prodConfirmed: true, CancellationToken.None);

        fs.Verify(x => x.Move(tmpPath, livePath, true), Times.Once);
    }
}
