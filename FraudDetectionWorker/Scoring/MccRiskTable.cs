namespace FraudDetectionWorker.Features
{
    /// <summary>
    /// Simple in-memory MCC risk lookup.
    /// In production this is typically a database table or configuration-driven rule set.
    /// </summary>
    public sealed class MccRiskTable
    {
        private static readonly Dictionary<string, float> RiskByCategory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GROCERY"] = 0.05f,
            ["RESTAURANT"] = 0.08f,
            ["FUEL"] = 0.10f,
            ["PHARMACY"] = 0.12f,
            ["SUBSCRIPTION"] = 0.15f,
            ["ELECTRONICS"] = 0.22f,
            ["TRAVEL"] = 0.25f,
            ["DIGITAL_GOODS"] = 0.30f,
            ["GAMING"] = 0.35f,
            ["LUXURY"] = 0.40f,
            ["GIFT_CARDS"] = 0.65f,
            ["CRYPTO"] = 0.80f,
            ["MONEY_TRANSFER"] = 0.85f
        };

        /// <summary>
        /// Returns a risk score in the range [0,1] with a safe default when unknown.
        /// </summary>
        public float GetRisk(string? merchantCategory)
            => merchantCategory is not null && RiskByCategory.TryGetValue(merchantCategory, out var v) ? v : 0.20f;
    }
}