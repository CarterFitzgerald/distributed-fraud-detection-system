namespace TransactionService.Models
{
    /// <summary>
    /// Represents a financial transaction within the system.
    /// This is the core domain entity used by the API and later persisted to storage.
    /// </summary>
    public class Transaction
    {
        /// <summary>
        /// Unique identifier for the transaction.
        /// Generated server-side.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Monetary value of the transaction.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// ISO currency code (e.g., AUD, USD).
        /// </summary>
        public string Currency { get; set; } = "AUD";

        /// <summary>
        /// Identifier of the merchant processing the transaction.
        /// </summary>
        public string MerchantId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the customer making the transaction.
        /// </summary>
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>
        /// Tokenized representation of the payment method.
        /// Raw card details are never stored.
        /// </summary>
        public string PaymentMethodToken { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the device used for the transaction.
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// ISO 3166-1 alpha-2 country code (e.g., AU, US).
        /// </summary>
        public string Country { get; set; } = "AU";

        /// <summary>
        /// Timestamp of when the transaction occurred.
        /// Defaults to UTC.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Fraud score from 0-100. Higher means more suspicious.
        /// </summary>
        public int? FraudScore { get; set; }

        /// <summary>
        /// Human-readable reason codes for why the score was assigned (demo / rule-based).
        /// Example: "HIGH_AMOUNT;OFF_HOURS".
        /// </summary>
        public string? FraudReason { get; set; }

        /// <summary>
        /// Timestamp when the fraud score was computed by the worker.
        /// </summary>
        public DateTimeOffset? FraudScoredAt { get; set; }

        /// <summary>
        /// Fraud Probability from 0.0 to 1.0. Higher means more likely to be fraudulent.
        /// </summary>
        public float? FraudProbability { get; set; }
        /// <summary>
        /// Predicted label from the fraud model given as True (fraud) or False (not fraud).
        /// </summary>
        public bool? FraudPrediction { get; set; }
        /// <summary>
        /// Version of the fraud model used to compute the score and prediction.
        /// </summary>s
        public string? FraudModelVersion { get; set; }
    }
}
