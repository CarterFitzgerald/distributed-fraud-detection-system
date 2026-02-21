using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Models;

namespace TransactionService.Services
{
    /// <summary>
    /// Entity Framework Core implementation of <see cref="ITransactionRepository"/>.
    /// Uses <see cref="AppDbContext"/> to interact with the SQL Server database.
    /// </summary>
    public class EfTransactionRepository : ITransactionRepository
    {
        private readonly AppDbContext _db;

        /// <summary>
        /// Constructor with dependency injection of the database context.
        /// </summary>
        public EfTransactionRepository(AppDbContext db)
        {
            _db = db;
        }

        /// <inheritdoc />
        public async Task<Transaction> AddAsync(Transaction transaction)
        {
            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync();
            return transaction;
        }

        /// <inheritdoc />
        public Task<Transaction?> GetByIdAsync(Guid id)
        {
            return _db.Transactions.FirstOrDefaultAsync(t => t.Id == id);
        }
    }
}
