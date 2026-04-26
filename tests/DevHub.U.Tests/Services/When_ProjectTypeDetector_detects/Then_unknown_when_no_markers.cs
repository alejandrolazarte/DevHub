using DevHub.Models;
using DevHub.Services;

namespace DevHub.U.Tests.Services.When_ProjectTypeDetector_detects;

public class Then_unknown_when_no_markers
{
    [Fact]
    public void Execute()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);

        var sut = new ProjectTypeDetector();
        var result = sut.Detect(dir);

        Assert.Equal(ProjectType.Unknown, result);
        Directory.Delete(dir, true);
    }
}