using Microsoft.AspNetCore.Mvc;
using TransactionService.Models;
using TransactionService.Services;

namespace TransactionService.Controllers
{
    /// <summary>
    /// API controller responsible for handling transaction-related requests.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionStore _store;

        /// <summary>
        /// Constructor with dependency injection.
        /// </summary>
        public TransactionsController(ITransactionStore store)
        {
            _store = store;
        }

        /// <summary>
        /// Creates a new transaction.
        /// Returns 201 Created with a link to retrieve the transaction.
        /// </summary>
        [HttpPost]
        public ActionResult<TransactionResponse> Create([FromBody] CreateTransactionRequest request)
        {
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

            _store.Add(tx);

            return CreatedAtAction(nameof(GetById), new { id = tx.Id }, ToResponse(tx));
        }

        /// <summary>
        /// Retrieves a transaction by its unique identifier.
        /// </summary>
        [HttpGet("{id:guid}")]
        public ActionResult<TransactionResponse> GetById(Guid id)
        {
            var tx = _store.Get(id);
            if (tx is null) return NotFound();

            return Ok(ToResponse(tx));
        }

        /// <summary>
        /// Maps domain entity to response DTO.
        /// Keeps controller logic clean and consistent.
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