using DevHub.Data;
using DevHub.Models;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DevHub.U.Tests.Services.When_RepoCommandsService_aggregates;

public class Then_all_sources_combined
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "angular.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(dir, "package.json"),
            """{"scripts":{"e2e":"cypress run"}}""");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(options);

        var sut = new RepoCommandsService(
            new ProjectTypeDetector(),
            new PackageJsonReader(),
            new HiddenCommandService(factory));

        var commands = await sut.GetAutoCommandsAsync(dir);

        Assert.Contains(commands, c => c.Source == CommandSource.AutoDetected && c.Name == "Serve");
        Assert.Contains(commands, c => c.Source == CommandSource.PackageJson && c.Name == "e2e");

        Directory.Delete(dir, true);
    }
}