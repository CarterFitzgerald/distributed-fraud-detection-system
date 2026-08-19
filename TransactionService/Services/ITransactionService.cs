using TransactionService.Models;

namespace TransactionService.Services
{
    /// <summary>
    /// Application service responsible for transaction-related business logic.
    /// Coordinates between the API layer (controllers) and the persistence layer (repository).
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        /// Creates a new transaction from the given request.
        /// </summary>
        Task<TransactionResponse> CreateAsync(CreateTransactionRequest request);

        /// <summary>
        /// Retrieves a transaction by its identifier.
        /// </summary>
        Task<TransactionResponse?> GetByIdAsync(Guid id);

        /// <summary>
        /// Runs a simulation by generating and submitting <paramref name="count"/> random
        /// transactions through the full pipeline, with a small delay between each
        /// to simulate realistic traffic flow.
        /// </summary>
        /// <param name="count">Number of transactions to generate. Capped at 2000.</param>
        /// <param name="delayMs">Delay in milliseconds between each submission.</param>
        /// <returns>List of IDs for the submitted transactions.</returns>
        Task<SimulationRunResult> RunSimulationAsync(int count, int delayMs = 20);

        /// <summary>
        /// Builds a summary report across all transactions currently in the database.
        /// </summary>
        Task<SimulationReport> GetSimulationReportAsync();

        /// <summary>
        /// Returns all scored transactions as a flat list for CSV export.
        /// </summary>
        Task<IEnumerable<Transaction>> GetAllScoredAsync();
    }
}