using TransactionService.Data;
using TransactionService.Models;

namespace TransactionService.Services
{
    /// <summary>
    /// Entity Framework Core implementation of <see cref="ITransactionStore"/>.
    /// Uses <see cref="AppDbContext"/> to persist transactions to a SQL database.
    /// </summary>
    public class EfTransactionStore : ITransactionStore
    {
        private readonly AppDbContext _db;

        /// <summary>
        /// Constructor with dependency injection of the database context.
        /// </summary>
        public EfTransactionStore(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Persists a new transaction to the database.
        /// </summary>
        /// <param name="tx">The transaction to persist.</param>
        /// <returns>The same transaction instance after being stored.</returns>
        public Transaction Add(Transaction tx)
        {
            _db.Transactions.Add(tx);
            _db.SaveChanges();
            return tx;
        }

        /// <summary>
        /// Retrieves a transaction by its unique identifier from the database.
        /// </summary>
        /// <param name="id">The transaction ID.</param>
        /// <returns>The transaction if found; otherwise null.</returns>
        public Transaction? Get(Guid id)
        {
            return _db.Transactions.FirstOrDefault(t => t.Id == id);
        }
    }
}