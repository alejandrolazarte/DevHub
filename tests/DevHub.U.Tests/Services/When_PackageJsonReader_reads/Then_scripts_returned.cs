using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_PackageJsonReader_reads;

public class Then_scripts_returned
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "package.json"),
            """{"scripts":{"start":"node index.js","build":"webpack","test":"jest"}}""");

        var sut = new PackageJsonReader();
        var result = sut.GetScripts(dir);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Name == "start" && c.Command == "npm run start");
        Assert.Contains(result, c => c.Name == "build" && c.Command == "npm run build");
        Assert.All(result, c => Assert.Equal(CommandSource.PackageJson, c.Source));
        Directory.Delete(dir, true);
    }
}