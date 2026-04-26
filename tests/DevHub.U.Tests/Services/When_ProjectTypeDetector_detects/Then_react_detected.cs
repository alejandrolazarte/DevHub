using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_react_detected
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "package.json"),
            """{"dependencies":{"react":"^18.0.0"}}""");

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.React, result);
        Directory.Delete(dir, true);
    }
}