using System.Collections.Concurrent;
using TransactionService.Models;

namespace TransactionService.Services
{
    /// <summary>
    /// Thread-safe in-memory implementation of transaction storage.
    /// Used for early development before introducing a database.
    /// </summary>
    public class InMemoryTransactionStore : ITransactionStore
    {
        // ConcurrentDictionary ensures thread safety under parallel requests
        private readonly ConcurrentDictionary<Guid, Transaction> _store = new();

        /// <summary>
        /// Adds a transaction to the in-memory store.
        /// </summary>
        public Transaction Add(Transaction tx)
        {
            _store[tx.Id] = tx;
            return tx;
        }

        /// <summary>
        /// Retrieves a transaction by its ID.
        /// Returns null if not found.
        /// </summary>
        public Transaction? Get(Guid id)
        {
            _store.TryGetValue(id, out var tx);
            return tx;
        }
    }
}
