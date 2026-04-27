using DevHub.Helpers;
using Shouldly;

namespace DevHub.U.Tests.Helpers.When_ConsoleHighlighter_tokenizes;

public class Then_url_gets_blue_color
{
    [Fact]
    public void Execute()
    {
        var segments = ConsoleHighlighter.Tokenize("Now listening on https://localhost:5001").ToList();

        var url = segments.FirstOrDefault(s => s.Text.StartsWith("https://"));
        url.Text.ShouldNotBeNull();
        url.Color.ShouldBe("#60a5fa");
    }
}
