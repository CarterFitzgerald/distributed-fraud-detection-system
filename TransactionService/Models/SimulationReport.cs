namespace TransactionService.Models
{
    /// <summary>
    /// Summary report produced after a simulation run.
    /// Returned by GET /api/transactions/simulation/report.
    /// </summary>
    public class SimulationReport
    {
        /// <summary>Total transactions submitted in the simulation.</summary>
        public int TotalSubmitted { get; set; }

        /// <summary>Transactions that have been scored by the fraud worker so far.</summary>
        public int TotalScored { get; set; }

        /// <summary>Transactions not yet scored (worker still processing).</summary>
        public int PendingScoring { get; set; }

        /// <summary>Number of transactions predicted as fraudulent.</summary>
        public int FraudCount { get; set; }

        /// <summary>Number of transactions predicted as legitimate.</summary>
        public int LegitimateCount { get; set; }

        /// <summary>Fraud rate as a percentage of scored transactions.</summary>
        public double FraudRatePercent { get; set; }

        /// <summary>Average fraud probability across all scored transactions.</summary>
        public double AverageFraudProbability { get; set; }

        /// <summary>Average fraud score across all scored transactions.</summary>
        public double AverageFraudScore { get; set; }

        /// <summary>Highest fraud probability seen in this batch.</summary>
        public double MaxFraudProbability { get; set; }

        /// <summary>Timestamp when this report was generated.</summary>
        public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Breakdown of fraud counts by merchant category.</summary>
        public Dictionary<string, CategoryBreakdown> ByMerchantCategory { get; set; } = new();
    }

    /// <summary>
    /// Per-category breakdown used inside <see cref="SimulationReport"/>.
    /// </summary>
    public class CategoryBreakdown
    {
        public int Total { get; set; }
        public int FraudCount { get; set; }
        public double FraudRatePercent { get; set; }
    }
}