using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_FolderPickerService_is_used;

public class Then_GetSubDirectories_returns_empty_on_error
{
    [Fact]
    public async Task Then_GetSubDirectories_returns_empty_on_error_Run()
    {
        var sut = new FolderPickerService();
        var result = sut.GetSubDirectories("C:/nonexistent-folder-12345");

        result.Count.ShouldBe(0);
    }
}

public class Then_GetParent_returns_null_at_root
{
    [Fact]
    public async Task Then_GetParent_returns_null_at_root_Run()
    {
        var sut = new FolderPickerService();
        var root = OperatingSystem.IsWindows() ? @"C:\" : "/";
        var result = sut.GetParent(root);

        result.ShouldBeNull();
    }
}