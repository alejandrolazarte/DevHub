using DevHub.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace DevHub.U.Tests.Services.When_ServiceBusMapService_regenerates;

public class Then_returns_failure_on_nonzero_exit
{
    [Fact]
    public async Task Then_returns_failure_on_nonzero_exit_Run()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ProcessResult(1, "", "boom"));

        var opts = Options.Create(new ServiceBusMapOptions());
        var env = Mock.Of<IHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath());
        var sut = new ServiceBusMapService(runner.Object, opts, env, NullLogger<ServiceBusMapService>.Instance);

        var result = await sut.RegenerateAsync();

        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("boom");
    }
}
