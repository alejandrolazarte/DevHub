using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoGroupResolver_resolves;

public class Then_prefix_match_returns_group_and_color
{
    [Fact]
    public void Then_matching_prefix_returns_configured_group_name_and_color()
    {
        var rules = new List<GroupRule>
        {
            new() { Name = "Core", Color = "primary", Prefixes = ["Core.", "DevHub."] }
        };

        var (group, color) = RepoGroupResolver.Resolve("Core.Api", rules);

        group.ShouldBe("Core");
        color.ShouldBe("primary");
    }
}
