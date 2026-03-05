namespace FraudDetectionWorker.Scoring
{
    /// <summary>
    /// ML.NET model input schema.
    /// 
    /// Important: numeric types must match the trainer schema (Single/float).
    /// </summary>
    public sealed class TransactionFeatures
    {
        // Categorical inputs (encoded via OneHot/Hash in the trainer pipeline)
        public string Country { get; set; } = "";
        public string Currency { get; set; } = "";
        public string MerchantId { get; set; } = "";
        public string CustomerId { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string PaymentMethodToken { get; set; } = "";

        public string TransactionType { get; set; } = "";
        public string Channel { get; set; } = "";
        public string MerchantCategory { get; set; } = "";
        public string DeviceType { get; set; } = "";

        // Numeric inputs
        public float Amount { get; set; }
        public float HourOfDay { get; set; }
        public float DayOfWeek { get; set; }

        // Boolean inputs (trainer converts these to floats)
        public bool IsNewDevice { get; set; }
        public bool IsNewPaymentToken { get; set; }
        public bool IsInternational { get; set; }

        // Engineered numeric features (trainer expects float)
        public float AccountAgeDays { get; set; }
        public float CustomerAge { get; set; }
        public float TxnCountLast1h { get; set; }
        public float TxnCountLast24h { get; set; }
        public float TotalAmountLast24h { get; set; }
        public float PaymentMethodAgeDays { get; set; }
        public float DistanceFromHomeKm { get; set; }
        public float MccRisk { get; set; }
    }
}