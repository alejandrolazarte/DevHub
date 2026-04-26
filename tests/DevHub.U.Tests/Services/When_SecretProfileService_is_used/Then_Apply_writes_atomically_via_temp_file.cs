using System.Text;
using DevHub.Services.SecretProfiles;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_Apply_writes_atomically_via_temp_file
{
    [Fact]
    public async Task Then_Apply_writes_atomically_via_temp_file_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        var profilePath = SutBuilder.ProfilePath("dev");
        var livePath = SutBuilder.LiveSecretsPath;
        var liveDir = Path.GetDirectoryName(livePath)!;
        var tmpPath = livePath + ".tmp";
        var bytes = Encoding.UTF8.GetBytes("{\"a\":1}");

        fs.Setup(x => x.FileExists(profilePath)).Returns(true);
        fs.Setup(x => x.DirectoryExists(liveDir)).Returns(true);
        fs.Setup(x => x.ReadAllBytesAsync(profilePath, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        fs.Setup(x => x.WriteAllBytesAsync(tmpPath, bytes, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        fs.Setup(x => x.Move(tmpPath, livePath, true));

        await sut.ApplyAsync(SutBuilder.SvcName, "dev", prodConfirmed: false, CancellationToken.None);

        fs.Verify(x => x.WriteAllBytesAsync(tmpPath, bytes, It.IsAny<CancellationToken>()), Times.Once);
        fs.Verify(x => x.Move(tmpPath, livePath, true), Times.Once);
    }
}
