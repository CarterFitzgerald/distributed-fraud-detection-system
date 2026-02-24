using System;

namespace TransactionService.Events
{

    /// <summary>
    /// Event published when a new transaction has been created and persisted.
    /// This is the payload sent to RabbitMQ.
    /// </summary>
    public class TransactionCreatedEvent
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = default!;
        public string MerchantId { get; set; } = default!;
        public string CustomerId { get; set; } = default!;
        public string Country { get; set; } = default!;
        public DateTimeOffset Timestamp { get; set; }
    }
}