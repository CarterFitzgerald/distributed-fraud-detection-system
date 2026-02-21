using TransactionService.Models;

namespace TransactionService.Services
{
    /// <summary>
    /// Default implementation of <see cref="ITransactionService"/>.
    /// Contains business logic such as mapping, normalization, and coordination
    /// between DTOs, domain entities, and the repository.
    /// </summary>
    public class TransactionAppService : ITransactionService
    {
        private readonly ITransactionRepository _repository;

        /// <summary>
        /// Constructor with dependency injection of the transaction repository.
        /// </summary>
        public TransactionAppService(ITransactionRepository repository)
        {
            _repository = repository;
        }

        /// <inheritdoc />
        public async Task<TransactionResponse> CreateAsync(CreateTransactionRequest request)
        {
            // Map incoming DTO to domain entity and apply basic normalization.
            var tx = new Transaction
            {
                Amount = request.Amount,
                Currency = request.Currency.ToUpperInvariant(),
                MerchantId = request.MerchantId,
                CustomerId = request.CustomerId,
                PaymentMethodToken = request.PaymentMethodToken,
                DeviceId = request.DeviceId,
                Country = request.Country.ToUpperInvariant(),
                Timestamp = request.Timestamp ?? DateTimeOffset.UtcNow
            };

            // Persist using the repository.
            var saved = await _repository.AddAsync(tx);

            // Map domain entity to response DTO.
            return ToResponse(saved);
        }

        /// <inheritdoc />
        public async Task<TransactionResponse?> GetByIdAsync(Guid id)
        {
            var tx = await _repository.GetByIdAsync(id);
            if (tx is null) return null;

            return ToResponse(tx);
        }

        /// <summary>
        /// Maps a domain transaction entity to a response DTO.
        /// Keeps mapping logic in one place.
        /// </summary>
        private static TransactionResponse ToResponse(Transaction tx) => new()
        {
            Id = tx.Id,
            Amount = tx.Amount,
            Currency = tx.Currency,
            MerchantId = tx.MerchantId,
            CustomerId = tx.CustomerId,
            PaymentMethodToken = tx.PaymentMethodToken,
            DeviceId = tx.DeviceId,
            Country = tx.Country,
            Timestamp = tx.Timestamp
        };
    }
}