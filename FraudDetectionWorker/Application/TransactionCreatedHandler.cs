using FraudDetectionWorker.Data;
using FraudDetectionWorker.Scoring;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DistributedFraud.Contracts.Events;

namespace FraudDetectionWorker.Application
{
    /// <summary>
    /// Handles TransactionCreated events:
    /// - Deserialize
    /// - Score
    /// - Persist fraud results
    /// </summary>
    public class TransactionCreatedHandler
    {
        private readonly IDbContextFactory<TransactionDbContext> _dbFactory;
        private readonly ILogger<TransactionCreatedHandler> _logger;

        public TransactionCreatedHandler(
            IDbContextFactory<TransactionDbContext> dbFactory,
            ILogger<TransactionCreatedHandler> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task HandleAsync(string payload, CancellationToken ct)
        {
            var evt = JsonSerializer.Deserialize<TransactionCreatedEvent>(payload);
            if (evt is null)
            {
                _logger.LogWarning("Invalid TransactionCreatedEvent payload. Skipping. Payload={Payload}", payload);
                return;
            }

            var (score, reason) = FraudScorer.Score(evt);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var row = await db.Transactions.FirstOrDefaultAsync(t => t.Id == evt.Id, ct);

            if (row is null)
            {
                _logger.LogWarning("Transaction {Id} not found. It may not be committed yet.", evt.Id);
                // For now: just return (message will be acked). If you prefer retry, throw.
                return;
            }

            if (row.FraudScoredAt is not null)
            {
                _logger.LogInformation("Transaction {Id} already scored. Score={Score}. Skipping.", row.Id, row.FraudScore);
                return;
            }

            row.FraudScore = score;
            row.FraudReason = reason;
            row.FraudScoredAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Scored transaction {Id}: Score={Score} Reason={Reason}", evt.Id, score, reason);
        }
    }
}