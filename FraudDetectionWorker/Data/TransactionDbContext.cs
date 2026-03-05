using Microsoft.EntityFrameworkCore;

namespace FraudDetectionWorker.Data
{
    /// <summary>
    /// Entity Framework Core context for transaction data, feature state, and enrichment tables.
    /// </summary>
    public class TransactionDbContext : DbContext
    {
        public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options) { }

        public DbSet<TransactionRow> Transactions => Set<TransactionRow>();
        public DbSet<CustomerProfileState> CustomerProfiles => Set<CustomerProfileState>();
        public DbSet<CustomerDeviceState> CustomerDevices => Set<CustomerDeviceState>();
        public DbSet<CustomerPaymentTokenState> CustomerPaymentTokens => Set<CustomerPaymentTokenState>();
        public DbSet<MerchantCategoryRisk> MerchantCategoryRisks => Set<MerchantCategoryRisk>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TransactionRow>(e =>
            {
                e.ToTable("Transactions");
                e.HasKey(t => t.Id);

                // Explicit column types prevent runtime casting issues when the SQL schema is DB-first.
                e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                e.Property(p => p.Timestamp).HasColumnType("datetimeoffset");

                e.Property(p => p.FraudScore).HasColumnType("int");
                e.Property(p => p.FraudScoredAt).HasColumnType("datetimeoffset");
                e.Property(p => p.FraudPrediction).HasColumnType("bit");
                e.Property(p => p.FraudProbability).HasColumnType("real");

                e.Property(p => p.AccountAgeDays).HasColumnType("int");
                e.Property(p => p.CustomerAge).HasColumnType("int");
                e.Property(p => p.PaymentMethodAgeDays).HasColumnType("int");

                e.Property(p => p.DistanceFromHomeKm).HasColumnType("float");
                e.Property(p => p.MccRisk).HasColumnType("float");
                e.Property(p => p.Latitude).HasColumnType("float");
                e.Property(p => p.Longitude).HasColumnType("float");

                e.Property(p => p.IsInternational).HasColumnType("bit");
                e.Property(p => p.IsNewDevice).HasColumnType("bit");
                e.Property(p => p.IsNewPaymentToken).HasColumnType("bit");

                e.Property(p => p.TotalAmountLast24h).HasColumnType("decimal(18,2)");
                e.Property(p => p.TxnCountLast1h).HasColumnType("int");
                e.Property(p => p.TxnCountLast24h).HasColumnType("int");

                // Query patterns used by feature engineering (velocity windows).
                e.HasIndex(t => new { t.CustomerId, t.Timestamp });
                e.HasIndex(t => new { t.MerchantId, t.Timestamp });
            });

            modelBuilder.Entity<CustomerProfileState>(e =>
            {
                e.ToTable("CustomerProfileState");
                e.HasKey(x => x.CustomerId);

                e.Property(p => p.CustomerId).HasColumnType("nvarchar(450)");
                e.Property(p => p.AccountCreatedAt).HasColumnType("datetimeoffset");
                e.Property(p => p.CustomerAgeYears).HasColumnType("int");
            });

            modelBuilder.Entity<CustomerDeviceState>(e =>
            {
                e.ToTable("CustomerDeviceState");
                e.HasKey(x => new { x.CustomerId, x.DeviceId });

                e.Property(p => p.CustomerId).HasColumnType("nvarchar(450)");
                e.Property(p => p.DeviceId).HasColumnType("nvarchar(450)");
                e.Property(p => p.FirstSeenAt).HasColumnType("datetimeoffset");
            });

            modelBuilder.Entity<CustomerPaymentTokenState>(e =>
            {
                e.ToTable("CustomerPaymentTokenState");
                e.HasKey(x => new { x.CustomerId, x.PaymentMethodToken });

                e.Property(p => p.CustomerId).HasColumnType("nvarchar(450)");
                e.Property(p => p.PaymentMethodToken).HasColumnType("nvarchar(450)");
                e.Property(p => p.FirstSeenAt).HasColumnType("datetimeoffset");
            });

            modelBuilder.Entity<MerchantCategoryRisk>(e =>
            {
                e.ToTable("MerchantCategoryRisk");
                e.HasKey(x => x.MerchantCategory);

                e.Property(p => p.MerchantCategory).HasColumnType("nvarchar(450)");
                e.Property(p => p.Risk).HasColumnType("float");
            });
        }
    }

    /// <summary>
    /// Transaction record including both raw transaction fields and engineered features/model outputs.
    /// </summary>
    public class TransactionRow
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }
        public string? Currency { get; set; }

        public string? MerchantId { get; set; }
        public string? CustomerId { get; set; }
        public string? PaymentMethodToken { get; set; }
        public string? DeviceId { get; set; }

        public string? Country { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        // Model outputs
        public string? FraudReason { get; set; }
        public int? FraudScore { get; set; }
        public DateTimeOffset? FraudScoredAt { get; set; }
        public string? FraudModelVersion { get; set; }
        public bool? FraudPrediction { get; set; }
        public float? FraudProbability { get; set; }

        // Engineered feature columns (persisted for analytics/debugging)
        public int? AccountAgeDays { get; set; }
        public string? Channel { get; set; }
        public int? CustomerAge { get; set; }
        public string? DeviceType { get; set; }
        public double? DistanceFromHomeKm { get; set; }
        public bool? IsInternational { get; set; }
        public bool? IsNewDevice { get; set; }
        public bool? IsNewPaymentToken { get; set; }
        public double? MccRisk { get; set; }
        public string? MerchantCategory { get; set; }
        public int? PaymentMethodAgeDays { get; set; }
        public decimal? TotalAmountLast24h { get; set; }
        public string? TransactionType { get; set; }
        public int? TxnCountLast1h { get; set; }
        public int? TxnCountLast24h { get; set; }

        // Optional enrichment fields
        public string? CustomerHomeCountry { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? MerchantRiskTier { get; set; }
    }

    /// <summary>
    /// Lightweight customer state used for computing features (e.g., account age, home country).
    /// </summary>
    public class CustomerProfileState
    {
        public string CustomerId { get; set; } = string.Empty;
        public string? HomeCountry { get; set; }
        public DateTimeOffset? AccountCreatedAt { get; set; }
        public int? CustomerAgeYears { get; set; }
    }

    /// <summary>
    /// Tracks first-seen device per customer to derive "new device" features.
    /// </summary>
    public class CustomerDeviceState
    {
        public string CustomerId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTimeOffset FirstSeenAt { get; set; }
    }

    /// <summary>
    /// Tracks first-seen payment token per customer to derive "new token" and "token age" features.
    /// </summary>
    public class CustomerPaymentTokenState
    {
        public string CustomerId { get; set; } = string.Empty;
        public string PaymentMethodToken { get; set; } = string.Empty;
        public DateTimeOffset FirstSeenAt { get; set; }
    }

    /// <summary>
    /// Lookup table mapping merchant categories to a risk score.
    /// </summary>
    public class MerchantCategoryRisk
    {
        public string MerchantCategory { get; set; } = string.Empty;
        public double Risk { get; set; }
    }
}