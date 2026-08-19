using TransactionService.Models;

namespace TransactionService.Services
{
    /// <summary>
    /// Repository abstraction for transaction persistence.
    /// </summary>
    public interface ITransactionRepository
    {
        /// <summary>Persists a new transaction and returns the saved entity.</summary>
        Task<Transaction> AddAsync(Transaction transaction);

        /// <summary>Retrieves a transaction by ID. Returns null if not found.</summary>
        Task<Transaction?> GetByIdAsync(Guid id);

        /// <summary>
        /// Retrieves all transactions in the database.
        /// Used by the simulation report and CSV export endpoints.
        /// </summary>
        Task<IEnumerable<Transaction>> GetAllAsync();
    }
}