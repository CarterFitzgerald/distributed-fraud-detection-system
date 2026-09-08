using TransactionService.Models;
using TransactionService.Messaging;
using TransactionService.Simulation;

namespace TransactionService.Services
{
    /// <summary>
    /// Default implementation of <see cref="ITransactionService"/>.
    /// </summary>
    public class TransactionAppService : ITransactionService
    {
        private readonly ITransactionRepository _repository;
        private readonly ITransactionEventPublisher _eventPublisher;
        private readonly ISimulationGroundTruthStore _groundTruthStore;


        public TransactionAppService(
            ITransactionRepository repository,
            ITransactionEventPublisher eventPublisher,
            ISimulationGroundTruthStore groundTruthStore)
        {
            _repository = repository;
            _eventPublisher = eventPublisher;
            _groundTruthStore = groundTruthStore;
        }

        /// <inheritdoc />
        public async Task<TransactionResponse> CreateAsync(CreateTransactionRequest request)
        {
            var tx = new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                Currency = request.Currency.ToUpperInvariant(),
                MerchantId = request.MerchantId,
                CustomerId = request.CustomerId,
                PaymentMethodToken = request.PaymentMethodToken,
                DeviceId = request.DeviceId,
                Country = request.Country.ToUpperInvariant(),
                Timestamp = request.Timestamp ?? DateTimeOffset.UtcNow,
                Channel = request.Channel,
                TransactionType = request.TransactionType,
                MerchantCategory = request.MerchantCategory,
                DeviceType = request.DeviceType,
                CustomerHomeCountry = request.CustomerHomeCountry,
                MerchantRiskTier = request.MerchantRiskTier,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                DistanceFromHomeKm = request.DistanceFromHomeKm,
            };

            var saved = await _repository.AddAsync(tx);
            await _eventPublisher.PublishTransactionCreatedAsync(saved);
            return ToResponse(saved);
        }

        /// <inheritdoc />
        public async Task<TransactionResponse?> GetByIdAsync(Guid id)
        {
            var tx = await _repository.GetByIdAsync(id);
            if (tx is null) return null;
            return ToResponse(tx);
        }

        /// <inheritdoc />
        public async Task<SimulationRunResult> RunSimulationAsync(int count, int delayMs = 20)
        {
            count = Math.Clamp(count, 1, 2000);
            _groundTruthStore.Reset();

            var ids = new List<Guid>(count);

            foreach (var simulated in TransactionSimulator.GenerateBatch(count))
            {
                var response = await CreateAsync(simulated.Request);
                _groundTruthStore.Record(response.Id, simulated.ExpectedFraud, simulated.Scenario);
                ids.Add(response.Id);

                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }

            return new SimulationRunResult
            {
                Submitted = ids.Count,
                TransactionIds = ids,
                EstimatedScoringSeconds = Math.Max(5, count / 50),
                ReportUrl = "/api/transactions/simulation/report"
            };
        }

        public async Task<SimulationAccuracyReport> GetSimulationAccuracyAsync()
        {
            var groundTruth = _groundTruthStore.Snapshot();
            var all = (await _repository.GetAllAsync()).ToList();

            var relevant = all.Where(t => groundTruth.ContainsKey(t.Id)).ToList();
            var scored = relevant.Where(t => t.FraudScoredAt.HasValue).ToList();

            int tp = 0, fp = 0, tn = 0, fn = 0;
            var scenarioStats = new Dictionary<string, (int Total, int Correct)>();

            foreach (var tx in scored)
            {
                var (expectedFraud, scenario) = groundTruth[tx.Id];
                var predictedFraud = tx.FraudPrediction == true;

                if (expectedFraud && predictedFraud) tp++;
                else if (!expectedFraud && predictedFraud) fp++;
                else if (!expectedFraud && !predictedFraud) tn++;
                else fn++;

                var correct = predictedFraud == expectedFraud;
                scenarioStats.TryGetValue(scenario, out var stats);
                scenarioStats[scenario] = (stats.Total + 1, stats.Correct + (correct ? 1 : 0));
            }

            var evaluated = scored.Count;
            var accuracy = evaluated > 0 ? (tp + tn) / (double)evaluated : 0;
            var precision = (tp + fp) > 0 ? tp / (double)(tp + fp) : 0;
            var recall = (tp + fn) > 0 ? tp / (double)(tp + fn) : 0;
            var f1 = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0;

            var report = new SimulationAccuracyReport
            {
                Evaluated = evaluated,
                PendingScoring = relevant.Count - evaluated,
                TruePositives = tp,
                FalsePositives = fp,
                TrueNegatives = tn,
                FalseNegatives = fn,
                Accuracy = Math.Round(accuracy * 100, 2),
                Precision = Math.Round(precision * 100, 2),
                Recall = Math.Round(recall * 100, 2),
                F1Score = Math.Round(f1 * 100, 2),
                GeneratedAt = DateTimeOffset.UtcNow
            };

            foreach (var (scenario, stats) in scenarioStats)
            {
                report.ByScenario[scenario] = new ScenarioAccuracy
                {
                    Total = stats.Total,
                    CorrectlyDetected = stats.Correct,
                    DetectionRatePercent = stats.Total > 0
                        ? Math.Round(stats.Correct / (double)stats.Total * 100, 2)
                        : 0
                };
            }

            return report;
        }

        /// <inheritdoc />
        public async Task<SimulationReport> GetSimulationReportAsync()
        {
            var all = (await _repository.GetAllAsync()).ToList();
            var scored = all.Where(t => t.FraudScoredAt.HasValue).ToList();

            var fraudulent = scored.Where(t => t.FraudPrediction == true).ToList();
            var legitimate = scored.Where(t => t.FraudPrediction == false).ToList();

            var report = new SimulationReport
            {
                TotalSubmitted = all.Count,
                TotalScored = scored.Count,
                PendingScoring = all.Count - scored.Count,
                FraudCount = fraudulent.Count,
                LegitimateCount = legitimate.Count,
                FraudRatePercent = scored.Count > 0
                    ? Math.Round(fraudulent.Count / (double)scored.Count * 100, 2)
                    : 0,
                AverageFraudProbability = scored.Count > 0
                    ? Math.Round(scored.Average(t => (double)(t.FraudProbability ?? 0)), 4)
                    : 0,
                AverageFraudScore = scored.Count > 0
                    ? Math.Round(scored.Average(t => (double)(t.FraudScore ?? 0)), 2)
                    : 0,
                MaxFraudProbability = scored.Count > 0
                    ? Math.Round(scored.Max(t => (double)(t.FraudProbability ?? 0)), 4)
                    : 0,
                GeneratedAt = DateTimeOffset.UtcNow
            };

            // Category breakdown
            var categories = scored
                .Where(t => t.MerchantCategory != null)
                .GroupBy(t => t.MerchantCategory!);

            foreach (var group in categories)
            {
                var groupFraud = group.Count(t => t.FraudPrediction == true);
                report.ByMerchantCategory[group.Key] = new CategoryBreakdown
                {
                    Total = group.Count(),
                    FraudCount = groupFraud,
                    FraudRatePercent = Math.Round(groupFraud / (double)group.Count() * 100, 2)
                };
            }

            return report;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Transaction>> GetAllScoredAsync()
        {
            var all = await _repository.GetAllAsync();
            return all.Where(t => t.FraudScoredAt.HasValue);
        }

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