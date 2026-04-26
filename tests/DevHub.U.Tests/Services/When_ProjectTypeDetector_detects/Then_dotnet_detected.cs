using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_dotnet_detected
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "MyApp.csproj"), "<Project />");

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.DotNet, result);
        Directory.Delete(dir, true);
    }
}