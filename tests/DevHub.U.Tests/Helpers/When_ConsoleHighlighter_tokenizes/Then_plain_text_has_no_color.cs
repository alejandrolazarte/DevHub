using DevHub.Helpers;
using Shouldly;

namespace DevHub.U.Tests.Helpers.When_ConsoleHighlighter_tokenizes;

public class Then_plain_text_has_no_color
{
    [Fact]
    public void Execute()
    {
        var segments = ConsoleHighlighter.Tokenize("Building project...").ToList();

        segments.ShouldAllBe(s => s.Color == null);
        string.Concat(segments.Select(s => s.Text)).ShouldBe("Building project...");
    }
}
