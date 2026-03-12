using Microsoft.EntityFrameworkCore;
using TransactionService.Models;

namespace TransactionService.Data
{
    /// <summary>
    /// Entity Framework Core database context for the application.
    /// Defines the tables (DbSets) that will be created in the database.
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// Constructor used by dependency injection to pass options (e.g. connection string).
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Represents the Transactions table in the database.
        /// Each <see cref="Transaction"/> instance corresponds to a row.
        /// </summary>
        public DbSet<Transaction> Transactions => Set<Transaction>();

        /// <summary>
        /// Configure entity mappings and database column settings.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.TotalAmountLast24h)
                .HasPrecision(18, 2);
        }
    }
}