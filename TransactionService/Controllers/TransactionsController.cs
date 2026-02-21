using Microsoft.AspNetCore.Mvc;
using TransactionService.Models;
using TransactionService.Services;

namespace TransactionService.Controllers
{
    /// <summary>
    /// API controller responsible for handling transaction-related HTTP requests.
    /// Delegates business logic to <see cref="ITransactionService"/>.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        /// <summary>
        /// Constructor with dependency injection of the transaction service.
        /// </summary>
        public TransactionsController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        /// Creates a new transaction.
        /// Returns 201 Created with a Location header pointing to the resource.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TransactionResponse>> Create([FromBody] CreateTransactionRequest request)
        {
            // Model validation is handled automatically by [ApiController] + data annotations.
            var created = await _transactionService.CreateAsync(request);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>
        /// Retrieves a transaction by its unique identifier.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TransactionResponse>> GetById(Guid id)
        {
            var result = await _transactionService.GetByIdAsync(id);
            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
    }
}