using DevHub.Helpers;
using Shouldly;

namespace DevHub.U.Tests.Helpers.When_ConsoleHighlighter_tokenizes;

public class Then_error_word_gets_red_color
{
    [Fact]
    public void Execute()
    {
        var segments = ConsoleHighlighter.Tokenize("Build error: file not found").ToList();

        var errorSeg = segments.FirstOrDefault(s => s.Text.Equals("error", StringComparison.OrdinalIgnoreCase));
        errorSeg.Color.ShouldBe("#f87171");
    }
}
