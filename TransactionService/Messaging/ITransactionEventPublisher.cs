using System.Threading.Tasks;
using TransactionService.Models;

namespace TransactionService.Messaging
{

    /// <summary>
    /// Abstraction for publishing transaction-related events to a message broker.
    /// </summary>
    public interface ITransactionEventPublisher
    {
        /// <summary>
        /// Publishes an event indicating that a transaction has been created.
        /// </summary>
        /// <param name="transaction">The created transaction.</param>
        Task PublishTransactionCreatedAsync(Transaction transaction);
    }
}