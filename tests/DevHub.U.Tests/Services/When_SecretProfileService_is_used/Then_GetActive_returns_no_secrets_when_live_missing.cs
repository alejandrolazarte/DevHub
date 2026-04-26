using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_GetActive_returns_no_secrets_when_live_missing
{
    [Fact]
    public async Task Then_GetActive_returns_no_secrets_when_live_missing_Run()
    {
        var (sut, fs) = SutBuilder.Build();
        fs.Setup(x => x.FileExists(SutBuilder.LiveSecretsPath)).Returns(false);

        var info = await sut.GetActiveProfileAsync(SutBuilder.SvcName, CancellationToken.None);

        info.LiveSecretsExists.ShouldBeFalse();
        info.MatchedProfileName.ShouldBeNull();
        info.IsDirty.ShouldBeFalse();
    }
}
