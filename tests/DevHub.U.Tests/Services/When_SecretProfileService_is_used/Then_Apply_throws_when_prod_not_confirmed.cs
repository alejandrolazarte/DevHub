using DevHub.Services.SecretProfiles;
using Shouldly;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

public class Then_Apply_throws_when_prod_not_confirmed
{
    [Fact]
    public async Task Then_Apply_throws_when_prod_not_confirmed_Run()
    {
        var (sut, _) = SutBuilder.Build();

        var act = () => sut.ApplyAsync(SutBuilder.SvcName, "prod", prodConfirmed: false, CancellationToken.None);

        await Should.ThrowAsync<ProdConfirmationRequiredException>(act);
    }
}
