using TransactionService.Models;

namespace TransactionService.Simulation
{
    /// <summary>
    /// Generates realistic transaction sequences for simulation.
    ///
    /// Design rationale:
    /// ------------------
    /// The fraud model relies on STATEFUL features computed by FraudDetectionWorker:
    ///   - IsNewDevice          → customer uses a device ID not seen before
    ///   - IsNewPaymentToken    → customer uses a payment token not seen before
    ///   - IsInternational      → tx.Country != customer home country
    ///   - TxnCountLast1h       → velocity: many txns in 1 hour window
    ///   - DistanceFromHomeKm   → geo anomaly signal
    ///   - MccRisk              → merchant category risk (GIFT_CARDS/LUXURY = high)
    ///
    /// If every transaction uses a random customer + device, no stateful signals
    /// ever trigger because each customer only appears once and has no history.
    ///
    /// Instead, this simulator uses a fixed pool of customers, each with:
    ///   - A home country
    ///   - 1-2 "known" devices and payment tokens (establish normal history)
    ///   - Fraud scenarios introduced after normal history is built up
    ///
    /// Fraud patterns simulated:
    ///   1. New device fraud     — known customer, new device ID + high amount
    ///   2. Card testing         — same customer, 5-8 small txns within 1 hour
    ///   3. International fraud  — transaction from a country different to home
    ///   4. High-risk merchant   — GIFT_CARDS or LUXURY with new payment token
    ///   5. Geo anomaly          — large DistanceFromHomeKm value
    /// </summary>
    public static class TransactionSimulator
    {
        private static readonly Random _rng = new(42); // Fixed seed for reproducibility

        // -----------------------------------------------------------------------
        // Customer pool — fixed identities with home countries and known devices
        // -----------------------------------------------------------------------

        private sealed record CustomerProfile(
            string CustomerId,
            string HomeCountry,
            string HomeCurrency,
            string[] KnownDevices,
            string[] KnownTokens,
            string[] FraudDevices,
            string[] FraudTokens
        );

        private static readonly CustomerProfile[] Customers =
        [
            BuildCustomer("c_001", "AU", "AUD"),
            BuildCustomer("c_002", "US", "USD"),
            BuildCustomer("c_003", "GB", "GBP"),
            BuildCustomer("c_004", "DE", "EUR"),
            BuildCustomer("c_005", "SG", "SGD"),
            BuildCustomer("c_006", "AU", "AUD"),
            BuildCustomer("c_007", "JP", "JPY"),
            BuildCustomer("c_008", "CA", "CAD"),
            BuildCustomer("c_009", "NZ", "NZD"),
            BuildCustomer("c_010", "FR", "EUR"),
        ];

        // Foreign countries used for international fraud (must differ from home)
        private static readonly Dictionary<string, string[]> ForeignCountries = new()
        {
            { "AU", new[] { "RO", "NG", "ID", "BR", "PK" } },
            { "US", new[] { "RO", "NG", "UA", "BR", "VN" } },
            { "GB", new[] { "NG", "RO", "UA", "BR", "PK" } },
            { "DE", new[] { "NG", "RO", "UA", "BR", "ID" } },
            { "SG", new[] { "NG", "RO", "UA", "BR", "PK" } },
            { "JP", new[] { "NG", "RO", "UA", "BR", "PK" } },
            { "CA", new[] { "NG", "RO", "UA", "BR", "ID" } },
            { "NZ", new[] { "NG", "RO", "UA", "BR", "PK" } },
            { "FR", new[] { "NG", "RO", "UA", "BR", "ID" } },
        };

        private static readonly string[] LowRiskCategories =
            ["GROCERY", "PHARMACY", "FUEL", "CLOTHING", "DINING"];

        private static readonly string[] HighRiskCategories =
            ["GIFT_CARDS", "LUXURY", "ELECTRONICS", "ONLINE_GAMING"];

        private static readonly string[] Channels =
            ["IN_STORE", "ECOM", "MOBILE_APP", "CONTACTLESS"];

        private static readonly string[] TransactionTypes =
            ["CARD_DEBIT", "CARD_CREDIT", "CONTACTLESS", "ONLINE_PURCHASE"];

        private static readonly string[] DeviceTypes =
            ["MOBILE", "DESKTOP", "POS_TERMINAL", "TABLET"];

        private static readonly string[] MerchantIds =
            Enumerable.Range(1, 30).Select(i => $"m_{i:D6}").ToArray();

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Generates a realistic sequence of transactions for simulation.
        ///
        /// Structure per 100 transactions (approximate):
        ///   ~60% — normal transactions across all customers (builds history)
        ///   ~10% — new device fraud
        ///   ~10% — card testing (velocity bursts)
        ///   ~10% — international fraud
        ///   ~10% — high-risk merchant + new token
        /// </summary>
        public static IEnumerable<SimulatedTransaction> GenerateBatch(int count)
        {
            var transactions = new List<SimulatedTransaction>(count);

            var historyCount = (int)(count * 0.40);
            for (var i = 0; i < historyCount; i++)
            {
                var customer = Customers[i % Customers.Length];
                transactions.Add(new SimulatedTransaction(BuildNormalTransaction(customer), false, "NORMAL"));
            }

            var remaining = count - historyCount;
            for (var i = 0; i < remaining; i++)
            {
                var customer = Customers[_rng.Next(Customers.Length)];
                var roll = _rng.NextDouble();

                var tx = roll switch
                {
                    < 0.97 => new SimulatedTransaction(BuildNormalTransaction(customer), false, "NORMAL"),      // 97%
                    < 0.9775 => new SimulatedTransaction(BuildNewDeviceFraud(customer), true, "NEW_DEVICE_FRAUD"),      // 0.75%
                    < 0.985 => new SimulatedTransaction(BuildCardTestingBurst(customer), true, "CARD_TESTING"),         // 0.75%
                    < 0.9925 => new SimulatedTransaction(BuildInternationalFraud(customer), true, "INTERNATIONAL_FRAUD"), // 0.75%
                    _ => new SimulatedTransaction(BuildHighRiskMerchantFraud(customer), true, "HIGH_RISK_MERCHANT"),      // 0.75%
                };

                transactions.Add(tx);
            }

            var phase2 = transactions.Skip(historyCount).OrderBy(_ => _rng.Next()).ToList();
            return transactions.Take(historyCount).Concat(phase2);
        }

        // -----------------------------------------------------------------------
        // Transaction builders
        // -----------------------------------------------------------------------

        /// <summary>
        /// Normal transaction — uses known device and payment token.
        /// Builds customer history in the state tables.
        /// </summary>
        private static CreateTransactionRequest BuildNormalTransaction(CustomerProfile customer)
        {
            return new CreateTransactionRequest
            {
                Amount = Round(10 + _rng.NextDouble() * 290),   // $10–$300
                Currency = customer.HomeCurrency,
                MerchantId = MerchantIds[_rng.Next(MerchantIds.Length)],
                CustomerId = customer.CustomerId,
                DeviceId = customer.KnownDevices[_rng.Next(customer.KnownDevices.Length)],
                PaymentMethodToken = customer.KnownTokens[_rng.Next(customer.KnownTokens.Length)],
                Country = customer.HomeCountry,
                CustomerHomeCountry = customer.HomeCountry,
                Channel = Channels[_rng.Next(Channels.Length)],
                TransactionType = TransactionTypes[_rng.Next(TransactionTypes.Length)],
                MerchantCategory = LowRiskCategories[_rng.Next(LowRiskCategories.Length)],
                DeviceType = DeviceTypes[_rng.Next(DeviceTypes.Length)],
                MerchantRiskTier = "LOW",
                DistanceFromHomeKm = _rng.NextDouble() * 30,    // Close to home
                Timestamp = RecentTimestamp(hoursBack: 24),
            };
        }

        /// <summary>
        /// New device fraud — known customer suddenly uses an unrecognised device.
        /// Triggers: IsNewDevice=true, high amount, often high-risk merchant.
        /// </summary>
        private static CreateTransactionRequest BuildNewDeviceFraud(CustomerProfile customer)
        {
            return new CreateTransactionRequest
            {
                Amount = Round(800 + _rng.NextDouble() * 2200),  // $800–$3000
                Currency = customer.HomeCurrency,
                MerchantId = MerchantIds[_rng.Next(MerchantIds.Length)],
                CustomerId = customer.CustomerId,
                DeviceId = customer.FraudDevices[_rng.Next(customer.FraudDevices.Length)],  // NEW device
                PaymentMethodToken = customer.KnownTokens[0],  // Known token (just new device)
                Country = customer.HomeCountry,
                CustomerHomeCountry = customer.HomeCountry,
                Channel = "ECOM",
                TransactionType = "ONLINE_PURCHASE",
                MerchantCategory = HighRiskCategories[_rng.Next(HighRiskCategories.Length)],
                DeviceType = "DESKTOP",
                MerchantRiskTier = "HIGH",
                DistanceFromHomeKm = _rng.NextDouble() * 50,
                Timestamp = RecentTimestamp(hoursBack: 6),
            };
        }

        /// <summary>
        /// Card testing burst — same customer submits multiple small transactions
        /// in a short window to test whether a stolen card works.
        /// Triggers: TxnCountLast1h spike, IsNewPaymentToken=true.
        /// Note: Returns a single transaction — caller should invoke this multiple
        /// times in quick succession for the same customer to build velocity.
        /// </summary>
        private static CreateTransactionRequest BuildCardTestingBurst(CustomerProfile customer)
        {
            // Small amounts typical of card testing ($1–$20)
            return new CreateTransactionRequest
            {
                Amount = Round(1 + _rng.NextDouble() * 19),
                Currency = customer.HomeCurrency,
                MerchantId = MerchantIds[_rng.Next(MerchantIds.Length)],
                CustomerId = customer.CustomerId,
                DeviceId = customer.KnownDevices[0],
                PaymentMethodToken = customer.FraudTokens[_rng.Next(customer.FraudTokens.Length)],  // NEW token
                Country = customer.HomeCountry,
                CustomerHomeCountry = customer.HomeCountry,
                Channel = "ECOM",
                TransactionType = "CARD_CREDIT",
                MerchantCategory = "GIFT_CARDS",
                DeviceType = "MOBILE",
                MerchantRiskTier = "HIGH",
                DistanceFromHomeKm = _rng.NextDouble() * 10,
                // Cluster timestamps within the last hour to spike TxnCountLast1h
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-_rng.Next(1, 55)),
            };
        }

        /// <summary>
        /// International fraud — known customer, transaction from a foreign country.
        /// Triggers: IsInternational=true, large DistanceFromHomeKm.
        /// </summary>
        private static CreateTransactionRequest BuildInternationalFraud(CustomerProfile customer)
        {
            var foreignOptions = ForeignCountries.TryGetValue(customer.HomeCountry, out var opts)
                ? opts
                : ["NG", "RO", "UA"];

            var foreignCountry = foreignOptions[_rng.Next(foreignOptions.Length)];

            return new CreateTransactionRequest
            {
                Amount = Round(500 + _rng.NextDouble() * 2500),   // $500–$3000
                Currency = "USD",   // Foreign currency
                MerchantId = MerchantIds[_rng.Next(MerchantIds.Length)],
                CustomerId = customer.CustomerId,
                DeviceId = customer.FraudDevices[0],    // Also new device
                PaymentMethodToken = customer.FraudTokens[0],  // Also new token
                Country = foreignCountry,               // DIFFERENT from home country
                CustomerHomeCountry = customer.HomeCountry,
                Channel = "ECOM",
                TransactionType = "ONLINE_PURCHASE",
                MerchantCategory = HighRiskCategories[_rng.Next(HighRiskCategories.Length)],
                DeviceType = "DESKTOP",
                MerchantRiskTier = "HIGH",
                DistanceFromHomeKm = 5000 + _rng.NextDouble() * 10000,  // Very far from home
                Timestamp = RecentTimestamp(hoursBack: 3),
            };
        }

        /// <summary>
        /// High-risk merchant fraud — known customer, new payment token, high-risk category.
        /// Triggers: IsNewPaymentToken=true, high MccRisk, high amount.
        /// </summary>
        private static CreateTransactionRequest BuildHighRiskMerchantFraud(CustomerProfile customer)
        {
            return new CreateTransactionRequest
            {
                Amount = Round(300 + _rng.NextDouble() * 1700),   // $300–$2000
                Currency = customer.HomeCurrency,
                MerchantId = MerchantIds[_rng.Next(MerchantIds.Length)],
                CustomerId = customer.CustomerId,
                DeviceId = customer.KnownDevices[0],
                PaymentMethodToken = customer.FraudTokens[_rng.Next(customer.FraudTokens.Length)],  // NEW token
                Country = customer.HomeCountry,
                CustomerHomeCountry = customer.HomeCountry,
                Channel = "ECOM",
                TransactionType = "ONLINE_PURCHASE",
                MerchantCategory = HighRiskCategories[_rng.Next(HighRiskCategories.Length)],
                DeviceType = DeviceTypes[_rng.Next(DeviceTypes.Length)],
                MerchantRiskTier = "HIGH",
                DistanceFromHomeKm = _rng.NextDouble() * 100,
                Timestamp = RecentTimestamp(hoursBack: 12),
            };
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static CustomerProfile BuildCustomer(string id, string homeCountry, string currency)
        {
            return new CustomerProfile(
                CustomerId: id,
                HomeCountry: homeCountry,
                HomeCurrency: currency,
                KnownDevices: [$"d_known_{id}_01", $"d_known_{id}_02"],
                KnownTokens: [$"pm_known_{id}_01", $"pm_known_{id}_02", $"pm_known_{id}_03"],
                FraudDevices: [$"d_fraud_{id}_{Guid.NewGuid().ToString("N")[..6]}",
                               $"d_fraud_{id}_{Guid.NewGuid().ToString("N")[..6]}"],
                FraudTokens: [$"pm_fraud_{id}_{Guid.NewGuid().ToString("N")[..8]}",
                              $"pm_fraud_{id}_{Guid.NewGuid().ToString("N")[..8]}"]
            );
        }

        private static DateTimeOffset RecentTimestamp(int hoursBack)
            => DateTimeOffset.UtcNow.AddSeconds(-_rng.Next(0, hoursBack * 3600));

        private static decimal Round(double value)
            => Math.Round((decimal)value, 2);
    }
}