namespace TransactionService.Models
{
    /// <summary>
    /// Represents the transaction data returned to clients.
    /// </summary>
    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string MerchantId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string PaymentMethodToken { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }
}
