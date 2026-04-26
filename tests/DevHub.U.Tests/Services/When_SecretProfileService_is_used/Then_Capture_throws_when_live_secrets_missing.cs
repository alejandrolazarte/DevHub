using DevHub.Services.SecretProfiles;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_Capture_throws_when_live_secrets_missing
{
    [Fact]
    public async Task Then_Capture_throws_when_live_secrets_missing_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        fs.Setup(x => x.FileExists(SutBuilder.LiveSecretsPath)).Returns(false);

        var act = () => sut.CaptureAsync(SutBuilder.SvcName, "dev", CancellationToken.None);

        await Should.ThrowAsync<LiveSecretsMissingException>(act);
    }
}
