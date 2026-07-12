using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Trading.Infrastructure.Data;

public class TradingDbContextFactory
    : IDesignTimeDbContextFactory<TradingDbContext>
{
    public TradingDbContext CreateDbContext(
        string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<TradingDbContext>();

        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=TradingDB_Dev;Trusted_Connection=True;TrustServerCertificate=True");

        return new TradingDbContext(
            optionsBuilder.Options);
    }
}