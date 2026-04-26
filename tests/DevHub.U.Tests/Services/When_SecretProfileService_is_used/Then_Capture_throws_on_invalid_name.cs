using DevHub.Services.SecretProfiles;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_Capture_throws_on_invalid_name
{
    [Theory]
    [InlineData("../evil")]
    [InlineData("has spaces")]
    [InlineData("")]
    [InlineData("wi/th/slashes")]
    public async Task Execute(string badName)
    {
        var (sut, _) = SutBuilder.Build();

        var act = () => sut.CaptureAsync(SutBuilder.SvcName, badName, CancellationToken.None);

        await Should.ThrowAsync<InvalidProfileNameException>(act);
    }
}
