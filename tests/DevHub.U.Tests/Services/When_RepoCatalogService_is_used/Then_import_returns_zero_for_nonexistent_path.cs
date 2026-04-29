using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_import_returns_zero_for_nonexistent_path(DbFixture db) : IClassFixture<DbFixture>
{
    [Fact]
    public async Task Execute()
    {
        var sut = new EfRepoCatalogService(db.Factory, NullLogger<EfRepoCatalogService>.Instance);

        var result = await sut.ImportFromRootAsync("/does/not/exist/anywhere");

        result.ShouldBe(0);
    }
}
