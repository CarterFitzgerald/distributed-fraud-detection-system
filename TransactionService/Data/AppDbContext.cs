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
    }
}