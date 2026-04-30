using DevHub.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DevHub.U.Tests.Helpers;

internal sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new ApplicationDbContext(_options);
        db.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
