using TransactionService.Models;

namespace TransactionService.Services
{
    /// <summary>
    /// Defines contract for transaction persistence.
    /// Allows storage implementation to be swapped (in-memory, SQL, etc.).
    /// </summary>
    public interface ITransactionStore
    {
        Transaction Add(Transaction tx);
        Transaction? Get(Guid id);
    }
}
