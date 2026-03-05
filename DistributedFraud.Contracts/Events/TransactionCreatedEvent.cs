namespace DistributedFraud.Contracts.Events
{
    /// <summary>
    /// Event published when a transaction is created.
    /// 
    /// This contract is shared between services (publisher/consumer) so message shape
    /// and semantics remain consistent across deployments.
    /// </summary>
    public sealed class TransactionCreatedEvent
    {
        /// <summary>Unique transaction identifier (also the primary key in the Transactions table).</summary>
        public Guid Id { get; set; }

        /// <summary>Transaction amount in the transaction currency.</summary>
        public decimal Amount { get; set; }

        /// <summary>ISO currency code (e.g., "AUD", "USD").</summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>Merchant identifier.</summary>
        public string MerchantId { get; set; } = string.Empty;

        /// <summary>Customer identifier.</summary>
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>Device identifier observed for this transaction.</summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>Token representing the payment instrument (e.g., tokenized card).</summary>
        public string PaymentMethodToken { get; set; } = string.Empty;

        /// <summary>ISO country code where the transaction occurred.</summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>Sales channel (e.g., "ECOM", "IN_STORE").</summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>Transaction type (e.g., "CARD_DEBIT", "BANK_TRANSFER").</summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>Merchant category (e.g., MCC label used for risk lookup/feature engineering).</summary>
        public string MerchantCategory { get; set; } = string.Empty;

        /// <summary>Device type (e.g., "MOBILE", "DESKTOP").</summary>
        public string DeviceType { get; set; } = string.Empty;

        /// <summary>Risk tier assigned to the merchant by upstream systems.</summary>
        public string MerchantRiskTier { get; set; } = string.Empty;

        /// <summary>Customer's home country (if known by the publisher).</summary>
        public string CustomerHomeCountry { get; set; } = string.Empty;

        /// <summary>Customer latitude at time of transaction (if available).</summary>
        public double? Latitude { get; set; }

        /// <summary>Customer longitude at time of transaction (if available).</summary>
        public double? Longitude { get; set; }

        /// <summary>Distance from customer's home location in kilometers (if computed upstream).</summary>
        public double? DistanceFromHomeKm { get; set; }

        /// <summary>Transaction timestamp (UTC recommended).</summary>
        public DateTimeOffset Timestamp { get; set; }
    }
}