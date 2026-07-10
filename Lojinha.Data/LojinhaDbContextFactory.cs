using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lojinha.Data;

public class LojinhaDbContextFactory : IDesignTimeDbContextFactory<LojinhaDbContext>
{
    public LojinhaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LojinhaDbContext>();
        optionsBuilder.UseSqlite("Data Source=lojinha.db");
        return new LojinhaDbContext(optionsBuilder.Options);
    }
}
