using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_FolderPickerService_is_used;

public class Then_GetSubDirectories_returns_sorted_dirs
{
    [Fact]
    public async Task Then_GetSubDirectories_returns_sorted_dirs_Run()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "z-folder"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "a-folder"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "m-folder"));

        var sut = new FolderPickerService();
        var result = sut.GetSubDirectories(tempRoot);

        result.Count.ShouldBe(3);
        result[0].ShouldEndWith("a-folder");
        result[1].ShouldEndWith("m-folder");
        result[2].ShouldEndWith("z-folder");

        Directory.Delete(tempRoot, recursive: true);
    }
}