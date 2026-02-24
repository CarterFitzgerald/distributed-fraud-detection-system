using TransactionService.Events;

namespace FraudDetectionWorker.Scoring
{
    /// <summary>
    /// Fake-first fraud scoring engine.
    /// Uses simple, explainable rules to produce a 0–100 score and reason codes.
    /// This is designed to be replaced later by an ML model.
    /// </summary>
    public static class FraudScorer
    {
        public static (int score, string reason) Score(TransactionCreatedEvent evt)
        {
            var points = 0;
            var reasons = new List<string>();

            // 1) Amount-based risk
            if (evt.Amount > 5000)
            {
                points += 40;
                reasons.Add("HIGH_AMOUNT");
            }
            else if (evt.Amount > 1000)
            {
                points += 25;
                reasons.Add("MEDIUM_AMOUNT");
            }
            else if (evt.Amount > 200)
            {
                points += 10;
                reasons.Add("ELEVATED_AMOUNT");
            }

            // 2) Country-based risk (demo list)
            var highRiskCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NG", "RU", "IR", "KP"
        };

            if (!string.IsNullOrWhiteSpace(evt.Country) && highRiskCountries.Contains(evt.Country))
            {
                points += 25;
                reasons.Add("HIGH_RISK_COUNTRY");
            }

            // 3) Off-hours (UTC-based demo)
            // In real life you'd use customer/merchant timezone, but UTC is fine for the demo.
            var hour = evt.Timestamp.UtcDateTime.Hour;
            if (hour >= 0 && hour <= 5)
            {
                points += 10;
                reasons.Add("OFF_HOURS");
            }

            // 4) Suspicious placeholder patterns (demo)
            if ((evt.MerchantId?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (evt.CustomerId?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                points += 10;
                reasons.Add("TEST_IDENTIFIERS");
            }

            // Clamp 0–100
            var score = Math.Min(100, Math.Max(0, points));
            var reason = reasons.Count == 0 ? "LOW_RISK" : string.Join(';', reasons);

            return (score, reason);
        }
    }
}