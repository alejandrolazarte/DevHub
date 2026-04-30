using DevHub.Data;
using DevHub.Services;
using DevHub.U.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DevHub.U.Tests.Services.When_RepoCatalogService_is_used;

public class Then_import_returns_zero_for_nonexistent_path
{
    [Fact]
    public async Task Execute()
    {
        var factory = TestDatabaseHelper.CreateInMemoryFactory();
        var sut = new EfRepoCatalogService(factory, NullLogger<EfRepoCatalogService>.Instance);

        var result = await sut.ImportFromRootAsync("/does/not/exist/anywhere");

        result.ShouldBe(0);
    }
}
