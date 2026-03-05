using System.ComponentModel.DataAnnotations;

namespace TransactionService.Models
{
    /// <summary>
    /// API request model used to create a new transaction.
    /// 
    /// Validation attributes enforce basic input constraints
    /// before the transaction is persisted or published as an event.
    /// </summary>
    public class CreateTransactionRequest
    {
        /// <summary>Transaction amount.</summary>
        [Required]
        [Range(0.01, 1_000_000)]
        public decimal Amount { get; set; }

        /// <summary>ISO currency code (e.g. AUD, USD).</summary>
        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "AUD";

        /// <summary>Merchant identifier.</summary>
        [Required]
        [StringLength(64)]
        public string MerchantId { get; set; } = string.Empty;

        /// <summary>Customer identifier.</summary>
        [Required]
        [StringLength(64)]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>Tokenized payment instrument.</summary>
        [Required]
        [StringLength(128)]
        public string PaymentMethodToken { get; set; } = string.Empty;

        /// <summary>Device identifier used for the transaction.</summary>
        [Required]
        [StringLength(64)]
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>ISO country code where the transaction occurred.</summary>
        [Required]
        [RegularExpression("^[A-Z]{2}$")]
        public string Country { get; set; } = "AU";

        /// <summary>
        /// Transaction timestamp.  
        /// If omitted, the API may assign the current server time.
        /// </summary>
        public DateTimeOffset? Timestamp { get; set; }

        // ----------------------------
        // Transaction context
        // ----------------------------

        /// <summary>
        /// Channel where the transaction occurred (e.g., ECOM, IN_STORE, ATM).
        /// </summary>
        [Required]
        [StringLength(32)]
        public string Channel { get; set; } = "IN_STORE";

        /// <summary>
        /// Transaction type (e.g., CARD_DEBIT, BANK_TRANSFER).
        /// </summary>
        [Required]
        [StringLength(32)]
        public string TransactionType { get; set; } = "CARD_CREDIT";

        /// <summary>
        /// Merchant category bucket used for risk scoring.
        /// </summary>
        [Required]
        [StringLength(64)]
        public string MerchantCategory { get; set; } = "GROCERY";

        /// <summary>
        /// Device class (e.g., MOBILE, DESKTOP, ATM).
        /// </summary>
        [Required]
        [StringLength(32)]
        public string DeviceType { get; set; } = "MOBILE";

        // ----------------------------
        // Customer / merchant context
        // ----------------------------

        /// <summary>
        /// Customer's registered home country.
        /// Used to determine international transaction signals.
        /// </summary>
        [Required]
        [RegularExpression("^[A-Z]{2}$")]
        public string CustomerHomeCountry { get; set; } = "AU";

        /// <summary>
        /// Optional merchant risk tier classification.
        /// </summary>
        [StringLength(64)]
        public string? MerchantRiskTier { get; set; }

        // ----------------------------
        // Optional geolocation inputs
        // ----------------------------

        /// <summary>Latitude of the transaction location.</summary>
        [Range(-90, 90)]
        public double? Latitude { get; set; }

        /// <summary>Longitude of the transaction location.</summary>
        [Range(-180, 180)]
        public double? Longitude { get; set; }

        /// <summary>
        /// Precomputed distance between the customer home location
        /// and transaction location (in kilometers).
        /// </summary>
        [Range(0, 20000)]
        public double? DistanceFromHomeKm { get; set; }
    }
}