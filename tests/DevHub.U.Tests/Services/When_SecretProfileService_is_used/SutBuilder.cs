using DevHub.Services.SecretProfiles;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DevHub.U.Tests.Services.When_SecretProfileService_is_used;

internal static class SutBuilder
{
    public const string SvcName = "SampleService";
    public const string UserSecretsId = "Company.Services.Sample.API.Secrets";
    private static readonly string DefaultContentRoot = Path.Combine(
        Path.GetTempPath(),
        "devhub-tests",
        "content-root");

    public static (SecretProfileService Sut, Mock<IFileSystem> Fs) Build(
        string? contentRoot = null,
        string profilesRoot = @"..\..\profiles",
        string[]? prodNames = null)
    {
        contentRoot ??= DefaultContentRoot;
        var fs = new Mock<IFileSystem>(MockBehavior.Strict);
        var opts = Options.Create(new SecretProfileOptions
        {
            ProfilesRoot = profilesRoot,
            Services =
            [
                new SecretProfileServiceConfig
                {
                    Name = SvcName,
                    UserSecretsId = UserSecretsId,
                    ProdProfileNames = (prodNames ?? ["prod"]).ToList(),
                }
            ],
        });
        var env = Mock.Of<IHostEnvironment>(e => e.ContentRootPath == contentRoot);
        var sut = new SecretProfileService(fs.Object, opts, env, NullLogger<SecretProfileService>.Instance);
        return (sut, fs);
    }

    public static string ProfilesDir =>
        Path.GetFullPath(Path.Combine(DefaultContentRoot, @"..\..\profiles", SvcName));

    public static string ProfilePath(string name) => Path.Combine(ProfilesDir, name + ".json");

    public static string LiveSecretsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", UserSecretsId, "secrets.json");
}
