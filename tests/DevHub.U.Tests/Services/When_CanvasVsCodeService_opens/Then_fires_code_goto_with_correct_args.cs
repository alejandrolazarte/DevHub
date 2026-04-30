using DevHub.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevHub.U.Tests.Services.When_CanvasVsCodeService_opens;

public class Then_fires_code_goto_with_correct_args
{
    [Fact]
    public async Task Execute()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, string.Empty, string.Empty));

        var sut = new CanvasVsCodeService(runner.Object, NullLogger<CanvasVsCodeService>.Instance);

        await sut.OpenFileAsync("/home/user/repo/Foo.cs", 42);

        runner.Verify(r => r.RunAsync(
            "code",
            It.Is<string>(args => args.Contains("--goto") && args.Contains("/home/user/repo/Foo.cs:42")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
