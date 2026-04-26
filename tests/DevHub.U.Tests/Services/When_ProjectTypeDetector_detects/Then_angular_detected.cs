using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_angular_detected
{
    [Fact]
    public async Task Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "angular.json"), "{}");

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.Angular, result);
        Directory.Delete(dir, true);
    }
}