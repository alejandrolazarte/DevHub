using DevHub.Data;
using Microsoft.EntityFrameworkCore;

namespace DevHub.U.Tests.Helpers;

public sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
    : IDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext() => new(options);
}
