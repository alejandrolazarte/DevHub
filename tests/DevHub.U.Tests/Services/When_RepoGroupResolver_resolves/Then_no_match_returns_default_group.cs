using DevHub.Models;
using DevHub.Services;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoGroupResolver_resolves;

public class Then_no_match_returns_default_group
{
    [Fact]
    public void Then_unmatched_name_returns_default_group_and_default_color()
    {
        var rules = new List<GroupRule>
        {
            new() { Name = "Core", Color = "primary", Prefixes = ["Core."] }
        };

        var (group, color) = RepoGroupResolver.Resolve("Unknown.Service", rules);

        group.ShouldBe("Other");
        color.ShouldBe("default");
    }
}
