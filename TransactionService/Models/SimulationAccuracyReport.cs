namespace TransactionService.Models
{
    /// <summary>
    /// Compares what the model actually predicted against the ground-truth labels
    /// the simulator assigned when generating each transaction. Unlike
    /// SimulationReport (which only shows prediction volume), this answers
    /// "was the model right?" Only covers transactions created via
    /// /api/transactions/simulate. Returned by GET /api/transactions/simulation/accuracy.
    /// </summary>
    public class SimulationAccuracyReport
    {
        public int Evaluated { get; set; }
        public int PendingScoring { get; set; }

        public int TruePositives { get; set; }
        public int FalsePositives { get; set; }
        public int TrueNegatives { get; set; }
        public int FalseNegatives { get; set; }

        public double Accuracy { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }

        public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Detection rate per injected fraud scenario (e.g. how often card-testing bursts were actually caught).</summary>
        public Dictionary<string, ScenarioAccuracy> ByScenario { get; set; } = new();
    }

    public class ScenarioAccuracy
    {
        public int Total { get; set; }
        public int CorrectlyDetected { get; set; }
        public double DetectionRatePercent { get; set; }
    }
}