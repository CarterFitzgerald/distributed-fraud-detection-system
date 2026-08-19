using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Models;
namespace TransactionService.Services
{
    public class EfTransactionRepository : ITransactionRepository
    {
        private readonly AppDbContext _db;

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

        /// <inheritdoc />
        public async Task<IEnumerable<Transaction>> GetAllAsync()
        {
            return await _db.Transactions.ToListAsync();
        }
    }
}