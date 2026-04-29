using DevHub.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DevHub.U.Tests.Helpers;

public sealed class DbFixture : IAsyncLifetime
{
    private SqliteConnection? _connection;

    public TestDbContextFactory Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        Factory = new TestDbContextFactory(options);
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
