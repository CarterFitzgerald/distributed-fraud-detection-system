namespace TransactionService.Models
{
    /// <summary>
    /// Result returned immediately after a simulation run is submitted.
    /// The fraud scoring happens asynchronously via the worker, so this
    /// confirms submission only — use the report endpoint to see results.
    /// </summary>
    public class SimulationRunResult
    {
        /// <summary>Number of transactions successfully submitted.</summary>
        public int Submitted { get; set; }

        /// <summary>IDs of all submitted transactions.</summary>
        public List<Guid> TransactionIds { get; set; } = new();

        /// <summary>Estimated seconds for the worker to finish scoring.</summary>
        public int EstimatedScoringSeconds { get; set; }

        /// <summary>Hint to the caller about where to poll for results.</summary>
        public string ReportUrl { get; set; } = "/api/transactions/simulation/report";
    }
}