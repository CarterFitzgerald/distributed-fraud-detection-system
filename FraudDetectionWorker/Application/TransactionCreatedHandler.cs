using DistributedFraud.Contracts.Events;
using FraudDetectionWorker.Data;
using FraudDetectionWorker.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace FraudDetectionWorker.Application
{
    /// <summary>
    /// Consumes <see cref="TransactionCreatedEvent"/> payloads, enriches the transaction with engineered features,
    /// runs ML inference, and persists fraud scoring results back to the database.
    /// </summary>
    public sealed class TransactionCreatedHandler
    {
        private readonly ILogger<TransactionCreatedHandler> _logger;
        private readonly IDbContextFactory<TransactionDbContext> _dbFactory;
        private readonly TransactionFeatureComputer _featureComputer;
        private readonly FraudModelPredictor _predictor;
        private readonly FraudModelOptions _modelOptions;

        public TransactionCreatedHandler(
            ILogger<TransactionCreatedHandler> logger,
            IDbContextFactory<TransactionDbContext> dbFactory,
            TransactionFeatureComputer featureComputer,
            FraudModelPredictor predictor,
            IOptions<FraudModelOptions> modelOptions)
        {
            _logger = logger;
            _dbFactory = dbFactory;
            _featureComputer = featureComputer;
            _predictor = predictor;
            _modelOptions = modelOptions.Value;
        }

        /// <summary>
        /// Handles a single message payload.
        /// 
        /// Notes:
        /// - The database is the source of truth for the transaction row.
        /// - The handler is idempotent: if a transaction is already scored, it exits early.
        /// </summary>
        public async Task HandleAsync(string payload, CancellationToken ct)
        {
            // Short correlation id to tie together logs across stages for a single message.
            var corr = Guid.NewGuid().ToString("N")[..8];
            var sw = Stopwatch.StartNew();

            try
            {
                if (string.IsNullOrWhiteSpace(payload))
                {
                    _logger.LogWarning("[{Corr}] Empty payload. Skipping.", corr);
                    return;
                }

                _logger.LogInformation("[{Corr}] HandleAsync start. payloadLen={Len}", corr, payload.Length);

                TransactionCreatedEvent evt;
                try
                {
                    evt = DeserializeTransactionCreatedEvent(payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[{Corr}] Failed to deserialize TransactionCreatedEvent. payload(first 500)={Payload}",
                        corr,
                        payload.Length > 500 ? payload[..500] : payload);

                    return;
                }

                if (evt is null || evt.Id == Guid.Empty)
                {
                    _logger.LogWarning("[{Corr}] Invalid event. Skipping.", corr);
                    return;
                }

                await using var db = await _dbFactory.CreateDbContextAsync(ct);

                // Load the transaction row from the database (DB is the system of record).
                var tx = await db.Transactions.FirstOrDefaultAsync(t => t.Id == evt.Id, ct);
                if (tx is null)
                {
                    _logger.LogWarning("[{Corr}] Transaction not found. txId={TxId}", corr, evt.Id);
                    return;
                }

                // Idempotency: do not rescore if already scored.
                if (tx.FraudScoredAt is not null)
                {
                    _logger.LogInformation(
                        "[{Corr}] Transaction already scored. txId={TxId} scoredAt={At:o}",
                        corr, tx.Id, tx.FraudScoredAt);

                    return;
                }

                // Apply event-provided hints only when the DB row is missing values.
                // This keeps the DB as source of truth while allowing the event to fill gaps.
                var applied = ApplyEventHints(tx, evt);

                if (applied > 0)
                {
                    _logger.LogInformation("[{Corr}] Applied {Count} event hint updates. Saving...", corr, applied);
                    await db.SaveChangesAsync(ct);
                }

                // Compute engineered features and persist state (e.g., device/token first-seen).
                _logger.LogInformation("[{Corr}] Stage=FeatureCompute begin. txId={TxId}", corr, tx.Id);
                var computed = await _featureComputer.ComputeAndPersistAsync(db, tx, ct);
                _logger.LogInformation("[{Corr}] Stage=FeatureCompute end. txId={TxId}", corr, tx.Id);

                // Run ML.NET inference using the trained model.
                _logger.LogInformation("[{Corr}] Stage=Predict begin. txId={TxId}", corr, tx.Id);
                var pred = _predictor.Predict(computed.Features);
                _logger.LogInformation(
                    "[{Corr}] Stage=Predict end. txId={TxId} prob={Prob:F6} rawScore={Raw:F6} label={Label}",
                    corr, tx.Id, pred.Probability, pred.Score, pred.PredictedLabel);

                // Persist outputs (prediction + metadata).
                tx.FraudProbability = pred.Probability;
                tx.FraudPrediction = pred.PredictedLabel;

                // "Score" here is a simple, human-readable risk score (0..1000).
                tx.FraudScore = (int)Math.Round(pred.Probability * 1000.0, MidpointRounding.AwayFromZero);

                tx.FraudReason = FraudReasonBuilder.Build(computed.Features, tx.FraudScore.Value);
                tx.FraudModelVersion = _modelOptions.ModelVersion;
                tx.FraudScoredAt = DateTimeOffset.UtcNow;

                _logger.LogInformation("[{Corr}] Stage=Persist begin. txId={TxId}", corr, tx.Id);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "[{Corr}] Stage=Persist end. txId={TxId} elapsedMs={Ms}",
                    corr, tx.Id, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("[{Corr}] HandleAsync canceled. elapsedMs={Ms}", corr, sw.ElapsedMilliseconds);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "[{Corr}] Database update failed. elapsedMs={Ms}", corr, sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Corr}] HandleAsync failed unexpectedly. elapsedMs={Ms}", corr, sw.ElapsedMilliseconds);
                throw;
            }
        }

        private static int ApplyEventHints(TransactionRow tx, TransactionCreatedEvent evt)
        {
            var applied = 0;

            if (string.IsNullOrWhiteSpace(tx.Channel) && !string.IsNullOrWhiteSpace(evt.Channel))
            { tx.Channel = evt.Channel; applied++; }

            if (string.IsNullOrWhiteSpace(tx.TransactionType) && !string.IsNullOrWhiteSpace(evt.TransactionType))
            { tx.TransactionType = evt.TransactionType; applied++; }

            if (string.IsNullOrWhiteSpace(tx.MerchantCategory) && !string.IsNullOrWhiteSpace(evt.MerchantCategory))
            { tx.MerchantCategory = evt.MerchantCategory; applied++; }

            if (string.IsNullOrWhiteSpace(tx.DeviceType) && !string.IsNullOrWhiteSpace(evt.DeviceType))
            { tx.DeviceType = evt.DeviceType; applied++; }

            if (string.IsNullOrWhiteSpace(tx.CustomerHomeCountry) && !string.IsNullOrWhiteSpace(evt.CustomerHomeCountry))
            { tx.CustomerHomeCountry = evt.CustomerHomeCountry; applied++; }

            if (string.IsNullOrWhiteSpace(tx.MerchantRiskTier) && !string.IsNullOrWhiteSpace(evt.MerchantRiskTier))
            { tx.MerchantRiskTier = evt.MerchantRiskTier; applied++; }

            if (tx.Latitude is null && evt.Latitude is not null)
            { tx.Latitude = evt.Latitude; applied++; }

            if (tx.Longitude is null && evt.Longitude is not null)
            { tx.Longitude = evt.Longitude; applied++; }

            if (tx.DistanceFromHomeKm is null && evt.DistanceFromHomeKm is not null)
            { tx.DistanceFromHomeKm = evt.DistanceFromHomeKm.Value; applied++; }

            return applied;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Deserializes an incoming message into a <see cref="TransactionCreatedEvent"/>.
        /// Supports common wrapper shapes used by brokers/tools:
        /// - { "message": { ... } }, { "data": { ... } }, { "event": { ... } }, { "payload": { ... } }
        /// - { "payload": "{...json...}" } where payload is a JSON string
        /// </summary>
        private static TransactionCreatedEvent DeserializeTransactionCreatedEvent(string payload)
        {
            var direct = JsonSerializer.Deserialize<TransactionCreatedEvent>(payload, JsonOpts);
            if (direct is not null && direct.Id != Guid.Empty) return direct;

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            foreach (var propName in new[] { "message", "data", "event", "payload" })
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propName, out var inner))
                {
                    var wrapped = inner.Deserialize<TransactionCreatedEvent>(JsonOpts);
                    if (wrapped is not null && wrapped.Id != Guid.Empty) return wrapped;
                }
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("payload", out var payloadProp) &&
                payloadProp.ValueKind == JsonValueKind.String)
            {
                var innerJson = payloadProp.GetString();
                if (!string.IsNullOrWhiteSpace(innerJson))
                {
                    var innerEvt = JsonSerializer.Deserialize<TransactionCreatedEvent>(innerJson, JsonOpts);
                    if (innerEvt is not null && innerEvt.Id != Guid.Empty) return innerEvt;
                }
            }

            return direct!;
        }
    }
}