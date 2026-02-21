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
        /// <param name="request">The incoming request DTO.</param>
        /// <returns>The created transaction as a response DTO.</returns>
        Task<TransactionResponse> CreateAsync(CreateTransactionRequest request);

        /// <summary>
        /// Retrieves a transaction by its identifier.
        /// </summary>
        /// <param name="id">The transaction ID.</param>
        /// <returns>
        /// The transaction as a response DTO if found; otherwise null.
        /// </returns>
        Task<TransactionResponse?> GetByIdAsync(Guid id);
    }
}