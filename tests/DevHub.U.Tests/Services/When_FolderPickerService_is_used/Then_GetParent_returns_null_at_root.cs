using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_FolderPickerService_is_used;

public class Then_GetParent_returns_null_at_root
{
    [Fact]
    public async Task Then_GetParent_returns_null_at_root_Run()
    {
        var sut = new FolderPickerService();
        var root = OperatingSystem.IsWindows() ? @"C:\\" : "/";
        var result = sut.GetParent(root);

        result.ShouldBeNull();
    }
}
