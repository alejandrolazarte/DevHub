using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_EfCanvasService_is_used;

public class Then_create_and_save_graph_persists(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new EfCanvasService(db.Factory, NullLogger<EfCanvasService>.Instance);

        var canvas = await sut.CreateAsync("My Board");
        canvas.Id.ShouldBeGreaterThan(0);
        canvas.Name.ShouldBe("My Board");

        var json = """{"elements":{"nodes":[{"data":{"id":"repo1"}}]}}""";
        await sut.SaveGraphAsync(canvas.Id, json);

        var loaded = await sut.GetByIdAsync(canvas.Id);
        loaded.ShouldNotBeNull();
        loaded!.CytoscapeJson.ShouldBe(json);
        loaded.UpdatedUtc.ShouldBeGreaterThanOrEqualTo(canvas.CreatedUtc);
    }
}
