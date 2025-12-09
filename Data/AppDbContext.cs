using Microsoft.EntityFrameworkCore;
using JwtAuthDemo.Models;

namespace JwtAuthDemo.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();


        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }
    }
}
