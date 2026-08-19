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

        public TransactionAppService(
            ITransactionRepository repository,
            ITransactionEventPublisher eventPublisher)
        {
            _repository = repository;
            _eventPublisher = eventPublisher;
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
            // Cap at 2000 to prevent accidental overload
            count = Math.Clamp(count, 1, 2000);

            var ids = new List<Guid>(count);

            foreach (var request in TransactionSimulator.GenerateBatch(count))
            {
                var response = await CreateAsync(request);
                ids.Add(response.Id);

                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }

            return new SimulationRunResult
            {
                Submitted = ids.Count,
                TransactionIds = ids,
                // Rough estimate: worker processes ~50 txns/sec
                EstimatedScoringSeconds = Math.Max(5, count / 50),
                ReportUrl = "/api/transactions/simulation/report"
            };
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