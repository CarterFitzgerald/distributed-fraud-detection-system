using TransactionService.Models;

namespace TransactionService.Services
{
    public interface ITransactionRepository
    {
        /// <summary>
        /// Persists a new transaction.
        /// </summary>
        /// <param name="transaction">The transaction to save.</param>
        /// <returns>The saved transaction instance.</returns>
        Task<Transaction> AddAsync(Transaction transaction);

        /// <summary>
        /// Retrieves a transaction by its unique identifier.
        /// </summary>
        /// <param name="id">The transaction ID.</param>
        /// <returns>The transaction if found; otherwise null.</returns>
        Task<Transaction?> GetByIdAsync(Guid id);
    }
}
