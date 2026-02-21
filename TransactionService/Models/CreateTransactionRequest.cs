using System.ComponentModel.DataAnnotations;

namespace TransactionService.Models
{
    /// <summary>
    /// Represents the payload required to create a new transaction.
    /// Validation attributes ensure API-level data integrity.
    /// </summary>
    public class CreateTransactionRequest
    {
        [Required]
        [Range(0.01, 1_000_000)]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "AUD";

        [Required]
        [StringLength(100)]
        public string MerchantId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string PaymentMethodToken { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        // AU, US, etc.
        [Required]
        [RegularExpression("^[A-Z]{2}$")]
        public string Country { get; set; } = "AU";

        /// <summary>
        /// Optional timestamp supplied by client.
        /// If null, server will assign current UTC time.
        /// </summary>
        public DateTimeOffset? Timestamp { get; set; }
    }
}
