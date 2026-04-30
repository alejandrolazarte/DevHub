using DevHub.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_CanvasRipgrepService_searches;

public class Then_returns_empty_when_rg_not_found
{
    [Fact]
    public async Task Execute()
    {
        var runner = new Mock<IProcessRunner>();
        runner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.ComponentModel.Win32Exception("No such file or directory"));

        var resolver = new Mock<IRipgrepResolverService>();
        resolver.Setup(r => r.GetRgPathAsync(It.IsAny<CancellationToken>())).ReturnsAsync("rg");

        var sut = new CanvasRipgrepService(runner.Object, resolver.Object, NullLogger<CanvasRipgrepService>.Instance);
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);

        try
        {
            var result = await sut.SearchAsync("IOrderService", [dir]);
            result.Matches.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
