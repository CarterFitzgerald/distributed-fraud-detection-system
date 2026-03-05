namespace FraudDetectionWorker.Scoring
{
    /// <summary>
    /// Produces a human-readable reason string to accompany a fraud score.
    /// This is intentionally lightweight and rule-based to aid debugging and auditability.
    /// </summary>
    public static class FraudReasonBuilder
    {
        public static string Build(TransactionFeatures f, int score0to1000)
        {
            if (f is null) throw new ArgumentNullException(nameof(f));

            var reasons = new List<string>();

            if (f.Amount >= 2000) reasons.Add("HIGH_AMOUNT");
            else if (f.Amount >= 500) reasons.Add("ELEVATED_AMOUNT");

            if (f.IsInternational) reasons.Add("INTERNATIONAL");
            if (f.IsNewDevice) reasons.Add("NEW_DEVICE");
            if (f.IsNewPaymentToken) reasons.Add("NEW_TOKEN");

            if (f.MccRisk >= 0.6f) reasons.Add("HIGH_RISK_MCC");
            if (f.TxnCountLast1h >= 6 || f.TxnCountLast24h >= 25) reasons.Add("HIGH_VELOCITY");
            if (f.TotalAmountLast24h >= 4000) reasons.Add("HIGH_24H_SPEND");

            if (reasons.Count == 0) reasons.Add("MODEL_ONLY");

            reasons.Add($"SCORE_{score0to1000}");
            return string.Join(';', reasons);
        }
    }
}