using Microsoft.AspNetCore.Mvc;
using System.Text;
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
            if (result is null) return NotFound();
            return Ok(result);
        }

        // -----------------------------------------------------------------------
        // Simulation endpoints
        // -----------------------------------------------------------------------

        /// <summary>
        /// Runs a transaction simulation by generating and submitting randomised
        /// transactions through the full pipeline (API → DB → RabbitMQ → Worker → ML scoring).
        ///
        /// Transactions are submitted with a small delay between each to simulate
        /// realistic traffic rather than a bulk insert. Fraud scoring is asynchronous —
        /// poll GET /api/transactions/simulation/report after the estimated scoring time.
        /// </summary>
        /// <param name="count">Number of transactions to generate. Default 1000, max 2000.</param>
        /// <param name="delayMs">Milliseconds between each submission. Default 20ms.</param>
        [HttpPost("simulate")]
        public async Task<ActionResult<SimulationRunResult>> RunSimulation(
            [FromQuery] int count = 1000,
            [FromQuery] int delayMs = 20)
        {
            if (count < 1 || count > 2000)
                return BadRequest("Count must be between 1 and 2000.");

            if (delayMs < 0 || delayMs > 1000)
                return BadRequest("DelayMs must be between 0 and 1000.");

            var result = await _transactionService.RunSimulationAsync(count, delayMs);
            return Ok(result);
        }

        /// <summary>
        /// Returns a summary fraud detection report across all transactions in the database.
        ///
        /// Includes total submitted, scored, fraud count, fraud rate, average fraud probability,
        /// and a breakdown by merchant category.
        ///
        /// Run POST /api/transactions/simulate first, then wait for the worker to score
        /// the transactions (see estimatedScoringSeconds in the simulate response).
        /// </summary>
        [HttpGet("simulation/report")]
        public async Task<ActionResult<SimulationReport>> GetSimulationReport()
        {
            var report = await _transactionService.GetSimulationReportAsync();
            return Ok(report);
        }

        /// <summary>
        /// Downloads a CSV file containing all scored transactions with key fraud fields.
        /// Useful for offline analysis and portfolio demonstration.
        /// </summary>
        [HttpGet("simulation/report/csv")]
        public async Task<IActionResult> DownloadSimulationCsv()
        {
            var transactions = (await _transactionService.GetAllScoredAsync()).ToList();

            if (!transactions.Any())
                return NotFound("No scored transactions found. Run a simulation first.");

            var csv = BuildCsv(transactions);
            var bytes = Encoding.UTF8.GetBytes(csv);
            var filename = $"fraud_simulation_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", filename);
        }

        /// <summary>
        /// Compares the model's predictions against the ground-truth labels the
        /// simulator assigned during the most recent run — i.e. did the model
        /// actually catch the injected fraud scenarios, and did it wrongly flag
        /// normal transactions? Only meaningful after POST /api/transactions/simulate.
        /// </summary>
        [HttpGet("simulation/accuracy")]
        public async Task<ActionResult<SimulationAccuracyReport>> GetSimulationAccuracy()
        {
            var report = await _transactionService.GetSimulationAccuracyAsync();
            return Ok(report);
        }

        // -----------------------------------------------------------------------
        // CSV builder
        // -----------------------------------------------------------------------

        private static string BuildCsv(IEnumerable<Transaction> transactions)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(
                "TransactionId,Timestamp,Amount,Currency,Country,MerchantId,CustomerId," +
                "MerchantCategory,Channel,TransactionType,DeviceType," +
                "IsInternational,IsNewDevice,IsNewPaymentToken," +
                "DistanceFromHomeKm,MccRisk," +
                "FraudPrediction,FraudProbability,FraudScore,FraudReason,FraudModelVersion,FraudScoredAt");

            foreach (var t in transactions)
            {
                sb.AppendLine(string.Join(",",
                    t.Id,
                    t.Timestamp.ToString("o"),
                    t.Amount,
                    t.Currency,
                    t.Country,
                    t.MerchantId,
                    t.CustomerId,
                    t.MerchantCategory ?? "",
                    t.Channel ?? "",
                    t.TransactionType ?? "",
                    t.DeviceType ?? "",
                    t.IsInternational?.ToString() ?? "",
                    t.IsNewDevice?.ToString() ?? "",
                    t.IsNewPaymentToken?.ToString() ?? "",
                    t.DistanceFromHomeKm?.ToString("F2") ?? "",
                    t.MccRisk?.ToString("F4") ?? "",
                    t.FraudPrediction?.ToString() ?? "",
                    t.FraudProbability?.ToString("F4") ?? "",
                    t.FraudScore?.ToString() ?? "",
                    $"\"{t.FraudReason?.Replace("\"", "'") ?? ""}\"",
                    t.FraudModelVersion ?? "",
                    t.FraudScoredAt?.ToString("o") ?? ""
                ));
            }

            return sb.ToString();
        }
    }
}