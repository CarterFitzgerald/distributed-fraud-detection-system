using Microsoft.EntityFrameworkCore;

namespace FraudDetectionWorker.Data
{
    /// <summary>
    /// Minimal EF Core DbContext used by the worker to update fraud scoring fields.
    /// This targets the same Transactions table created by TransactionService migrations.
    /// </summary>
    public class TransactionDbContext : DbContext
    {
        public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options) { }

        public DbSet<TransactionRow> Transactions => Set<TransactionRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionRow>().ToTable("Transactions");
            modelBuilder.Entity<TransactionRow>().HasKey(t => t.Id);
        }
    }

    /// <summary>
    /// Minimal transaction row mapping used by the worker.
    /// Only includes fields needed for fraud scoring updates.
    /// </summary>
    public class TransactionRow
    {
        public Guid Id { get; set; }
        public int? FraudScore { get; set; }
        public string? FraudReason { get; set; }
        public DateTimeOffset? FraudScoredAt { get; set; }
    }
}