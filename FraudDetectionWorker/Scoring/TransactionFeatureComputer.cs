using FraudDetectionWorker.Data;
using Microsoft.EntityFrameworkCore;

namespace FraudDetectionWorker.Scoring
{
    /// <summary>
    /// Computes engineered features for a transaction and persists feature/state columns needed
    /// for downstream scoring and monitoring.
    /// </summary>
    public sealed class TransactionFeatureComputer
    {
        private readonly ILogger<TransactionFeatureComputer> _logger;

        public TransactionFeatureComputer(ILogger<TransactionFeatureComputer> logger)
        {
            _logger = logger;
        }

        public sealed record FeatureResult(TransactionFeatures Features);

        public async Task<FeatureResult> ComputeAndPersistAsync(TransactionDbContext db, TransactionRow tx, CancellationToken ct)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));
            if (tx is null) throw new ArgumentNullException(nameof(tx));

            // --------------------
            // Customer profile state
            // --------------------
            CustomerProfileState? profile = null;

            if (!string.IsNullOrWhiteSpace(tx.CustomerId))
            {
                profile = await db.CustomerProfiles
                    .FirstOrDefaultAsync(x => x.CustomerId == tx.CustomerId, ct);

                if (profile is null)
                {
                    profile = new CustomerProfileState
                    {
                        CustomerId = tx.CustomerId!,
                        AccountCreatedAt = tx.Timestamp,
                        CustomerAgeYears = Random.Shared.Next(18, 91), // 18..90 inclusive
                        HomeCountry = FirstNonEmpty(tx.CustomerHomeCountry, tx.Country)
                    };

                    db.CustomerProfiles.Add(profile);

                    _logger.LogInformation(
                        "Created customer profile. cust={Cust} ageYears={Age} home={Home}",
                        profile.CustomerId, profile.CustomerAgeYears, profile.HomeCountry);
                }
                else
                {
                    // Best-effort backfill for partially-populated profiles.
                    profile.AccountCreatedAt ??= tx.Timestamp;
                    profile.CustomerAgeYears ??= Random.Shared.Next(18, 91);
                    profile.HomeCountry ??= FirstNonEmpty(tx.CustomerHomeCountry, tx.Country);
                }
            }
            else
            {
                _logger.LogWarning("Transaction missing CustomerId; profile-based features will default. txId={TxId}", tx.Id);
            }

            var homeCountry = FirstNonEmpty(tx.CustomerHomeCountry, profile?.HomeCountry, tx.Country);

            tx.AccountAgeDays ??= profile?.AccountCreatedAt is not null
                ? (int)Math.Max(0.0, (tx.Timestamp - profile.AccountCreatedAt.Value).TotalDays)
                : 0;

            tx.CustomerAge ??= profile?.CustomerAgeYears ?? 0;

            // --------------------
            // Merchant category risk (DB lookup with safe default)
            // --------------------
            var mccRisk = tx.MccRisk ?? 0.20d;

            if (!string.IsNullOrWhiteSpace(tx.MerchantCategory))
            {
                var mcc = await db.MerchantCategoryRisks
                    .FirstOrDefaultAsync(x => x.MerchantCategory == tx.MerchantCategory, ct);

                if (mcc is not null)
                    mccRisk = mcc.Risk;
            }

            // --------------------
            // Location / novelty signals
            // --------------------
            var isInternational =
                !string.IsNullOrWhiteSpace(homeCountry) &&
                !string.IsNullOrWhiteSpace(tx.Country) &&
                !string.Equals(homeCountry, tx.Country, StringComparison.OrdinalIgnoreCase);

            var isNewDevice = false;
            if (!string.IsNullOrWhiteSpace(tx.CustomerId) && !string.IsNullOrWhiteSpace(tx.DeviceId))
            {
                var dev = await db.CustomerDevices.FirstOrDefaultAsync(
                    x => x.CustomerId == tx.CustomerId && x.DeviceId == tx.DeviceId, ct);

                if (dev is null)
                {
                    isNewDevice = true;
                    db.CustomerDevices.Add(new CustomerDeviceState
                    {
                        CustomerId = tx.CustomerId!,
                        DeviceId = tx.DeviceId!,
                        FirstSeenAt = tx.Timestamp
                    });
                }
            }

            var isNewToken = false;
            var pmAgeDays = 0;

            if (!string.IsNullOrWhiteSpace(tx.CustomerId) && !string.IsNullOrWhiteSpace(tx.PaymentMethodToken))
            {
                var tok = await db.CustomerPaymentTokens.FirstOrDefaultAsync(
                    x => x.CustomerId == tx.CustomerId && x.PaymentMethodToken == tx.PaymentMethodToken, ct);

                if (tok is null)
                {
                    isNewToken = true;
                    pmAgeDays = 0;

                    db.CustomerPaymentTokens.Add(new CustomerPaymentTokenState
                    {
                        CustomerId = tx.CustomerId!,
                        PaymentMethodToken = tx.PaymentMethodToken!,
                        FirstSeenAt = tx.Timestamp
                    });
                }
                else
                {
                    pmAgeDays = (int)Math.Max(0.0, (tx.Timestamp - tok.FirstSeenAt).TotalDays);
                }
            }

            // --------------------
            // Velocity aggregates (requires index on CustomerId + Timestamp for scale)
            // --------------------
            var end = tx.Timestamp;
            var start1h = end.AddHours(-1);
            var start24h = end.AddHours(-24);

            var txnsLast1hQuery = db.Transactions
                .Where(t => t.CustomerId == tx.CustomerId && t.Timestamp >= start1h && t.Timestamp <= end);

            var txnsLast24hQuery = db.Transactions
                .Where(t => t.CustomerId == tx.CustomerId && t.Timestamp >= start24h && t.Timestamp <= end);

            var txnCount1h = await txnsLast1hQuery.CountAsync(ct);
            var txnCount24h = await txnsLast24hQuery.CountAsync(ct);

            var totalAmount24hDecimal = await txnsLast24hQuery.SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

            // --------------------
            // Persist feature columns on the transaction row
            // --------------------
            tx.IsInternational = isInternational;
            tx.IsNewDevice = isNewDevice;
            tx.IsNewPaymentToken = isNewToken;

            tx.PaymentMethodAgeDays = pmAgeDays;
            tx.MccRisk = mccRisk;

            tx.TxnCountLast1h = txnCount1h;
            tx.TxnCountLast24h = txnCount24h;

            tx.TotalAmountLast24h = totalAmount24hDecimal;

            tx.DistanceFromHomeKm ??= 0d;

            await db.SaveChangesAsync(ct);

            // --------------------
            // Build ML.NET input object (trainer expects float/Single numerics)
            // --------------------
            var utc = tx.Timestamp.UtcDateTime;

            var features = new TransactionFeatures
            {
                Amount = (float)tx.Amount,

                Country = tx.Country ?? "",
                Currency = tx.Currency ?? "",

                MerchantId = tx.MerchantId ?? "",
                CustomerId = tx.CustomerId ?? "",
                DeviceId = tx.DeviceId ?? "",
                PaymentMethodToken = tx.PaymentMethodToken ?? "",

                TransactionType = tx.TransactionType ?? "",
                Channel = tx.Channel ?? "",
                MerchantCategory = tx.MerchantCategory ?? "",
                DeviceType = tx.DeviceType ?? "",

                HourOfDay = utc.Hour,
                DayOfWeek = MapDayOfWeekMonday0(utc.DayOfWeek),

                IsInternational = isInternational,
                IsNewDevice = isNewDevice,
                IsNewPaymentToken = isNewToken,

                AccountAgeDays = (float)(tx.AccountAgeDays ?? 0),
                CustomerAge = (float)(tx.CustomerAge ?? 0),

                TxnCountLast1h = txnCount1h,
                TxnCountLast24h = txnCount24h,

                TotalAmountLast24h = (float)totalAmount24hDecimal,
                PaymentMethodAgeDays = pmAgeDays,
                DistanceFromHomeKm = (float)(tx.DistanceFromHomeKm ?? 0d),
                MccRisk = (float)mccRisk,
            };

            return new FeatureResult(features);
        }

        private static int MapDayOfWeekMonday0(DayOfWeek d) =>
            d switch
            {
                DayOfWeek.Monday => 0,
                DayOfWeek.Tuesday => 1,
                DayOfWeek.Wednesday => 2,
                DayOfWeek.Thursday => 3,
                DayOfWeek.Friday => 4,
                DayOfWeek.Saturday => 5,
                DayOfWeek.Sunday => 6,
                _ => 0
            };

        private static string? FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        /// <summary>
        /// Geodesic distance (km). Not currently used, but retained for future enhancement
        /// when computing distance from (lat, lon) instead of receiving it upstream.
        /// </summary>
        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;

            static double ToRad(double deg) => deg * Math.PI / 180.0;

            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}