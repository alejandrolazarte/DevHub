using DevHub.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_ServiceBusMapService_regenerates;

public class Then_returns_success_on_exit_zero
{
    [Fact]
    public async Task Then_returns_success_on_exit_zero_Run()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ProcessResult(0, "map generated", ""));

        var opts = Options.Create(new ServiceBusMapOptions());
        var env = Mock.Of<IHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath());
        var sut = new ServiceBusMapService(runner.Object, opts, env, NullLogger<ServiceBusMapService>.Instance);

        var result = await sut.RegenerateAsync();

        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("map generated");
    }
}
