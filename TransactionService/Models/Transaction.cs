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
    }
}
