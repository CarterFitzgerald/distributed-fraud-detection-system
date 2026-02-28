namespace DistributedFraud.Contracts.Events
{
    /// <summary>
    /// Event contract published when a transaction is created.
    /// Shared between services so publisher/consumer stay consistent.
    /// </summary>
    public sealed class TransactionCreatedEvent
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string MerchantId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }
}