using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FraudModelTrainer.OptionA
{
    /// <summary>
    /// Training pipeline for the fraud detection model used by the distributed system.
    ///
    /// Purpose:
    /// - Train a binary classifier that outputs fraud probability for a transaction.
    /// - Export a model (model.zip) consumed by FraudDetectionWorker for real-time inference.
    ///
    /// Data:
    /// - This trainer expects synthetic, system-aligned CSV files produced by the project generator:
    ///   • transactions_train.csv
    ///   • transactions_test.csv
    ///
    /// Approach:
    /// - Categorical fields are encoded using:
    ///   • OneHotEncoding for low-cardinality categories (e.g., country, channel)
    ///   • OneHotHashEncoding for high-cardinality identifiers (e.g., customerId, deviceId)
    /// - Booleans are converted to floats for compatibility with LightGBM.
    /// - Numeric features are normalized to improve training stability.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Input schema matching the synthetic transaction CSV header.
        /// All numeric fields are floats to align with ML.NET pipelines and LightGBM expectations.
        /// </summary>
        private sealed class TransactionTrainingRow
        {
            [LoadColumn(0)] public string TransactionId { get; set; } = string.Empty;
            [LoadColumn(1)] public float Amount { get; set; }

            [LoadColumn(2)] public string Country { get; set; } = string.Empty;
            [LoadColumn(3)] public string Currency { get; set; } = string.Empty;

            [LoadColumn(4)] public string MerchantId { get; set; } = string.Empty;
            [LoadColumn(5)] public string CustomerId { get; set; } = string.Empty;
            [LoadColumn(6)] public string DeviceId { get; set; } = string.Empty;
            [LoadColumn(7)] public string PaymentMethodToken { get; set; } = string.Empty;

            [LoadColumn(8)] public float HourOfDay { get; set; }
            [LoadColumn(9)] public float DayOfWeek { get; set; }

            [LoadColumn(10)] public string TransactionType { get; set; } = string.Empty;
            [LoadColumn(11)] public string Channel { get; set; } = string.Empty;
            [LoadColumn(12)] public string MerchantCategory { get; set; } = string.Empty;
            [LoadColumn(13)] public string DeviceType { get; set; } = string.Empty;

            [LoadColumn(14)] public bool IsNewDevice { get; set; }
            [LoadColumn(15)] public bool IsNewPaymentToken { get; set; }
            [LoadColumn(16)] public bool IsInternational { get; set; }

            [LoadColumn(17)] public float AccountAgeDays { get; set; }
            [LoadColumn(18)] public float CustomerAge { get; set; }

            [LoadColumn(19)] public float TxnCountLast1h { get; set; }
            [LoadColumn(20)] public float TxnCountLast24h { get; set; }
            [LoadColumn(21)] public float TotalAmountLast24h { get; set; }
            [LoadColumn(22)] public float PaymentMethodAgeDays { get; set; }
            [LoadColumn(23)] public float DistanceFromHomeKm { get; set; }
            [LoadColumn(24)] public float MccRisk { get; set; }

            // These can be useful for analysis but are not used directly in the model features here.
            [LoadColumn(25)] public bool IsHighRiskMcc { get; set; }
            [LoadColumn(26)] public bool IsHighVelocity { get; set; }
            [LoadColumn(27)] public bool IsSuspiciousCombo { get; set; }

            // Model label: true = fraud, false = legitimate.
            [LoadColumn(28)] public bool Label { get; set; }
        }

        /// <summary>
        /// Projection used for top-k evaluation.
        /// </summary>
        private sealed class ScoreRow
        {
            public bool Label { get; set; }
            public float Probability { get; set; }
            public float Score { get; set; }

            [ColumnName("PredictedLabel")]
            public bool PredictedLabel { get; set; }
        }

        public static void Main()
        {
            var ml = new MLContext(seed: 42);

            // ------------------------------------------------------------
            // Paths and input validation
            // ------------------------------------------------------------

            var projectDir = FindProjectDirectory();
            var dataDir = Path.Combine(projectDir, "Data");

            var trainPath = Path.Combine(dataDir, "transactions_train.csv");
            var testPath = Path.Combine(dataDir, "transactions_test.csv");

            var modelDir = Path.Combine(projectDir, "Model");
            Directory.CreateDirectory(modelDir);

            var modelPath = Path.Combine(modelDir, "model.zip");
            var infoPath = Path.Combine(modelDir, "model_info.json");

            Console.WriteLine($"Loading train: {trainPath}");
            Console.WriteLine($"Loading test : {testPath}");

            if (!File.Exists(trainPath) || !File.Exists(testPath))
            {
                Console.WriteLine("❌ Train/test CSV not found in FraudModelTrainer.OptionA/Data/");
                Environment.Exit(1);
            }

            var trainData = ml.Data.LoadFromTextFile<TransactionTrainingRow>(
                trainPath, hasHeader: true, separatorChar: ',', allowQuoting: true, trimWhitespace: true);

            var testData = ml.Data.LoadFromTextFile<TransactionTrainingRow>(
                testPath, hasHeader: true, separatorChar: ',', allowQuoting: true, trimWhitespace: true);

            PrintLabelDistribution(ml, trainData, "TRAIN");
            PrintLabelDistribution(ml, testData, "TEST");

            // ------------------------------------------------------------
            // Feature engineering pipeline
            //
            // Design:
            // - OneHotEncoding for small vocab categorical columns
            // - OneHotHashEncoding for high-cardinality identifiers
            // - Convert bool -> float for tree model compatibility
            // - Normalize numeric features to reduce scale issues
            //
            // Note: TransactionId is intentionally excluded from features.
            // ------------------------------------------------------------

            var pipeline =
                // Low-cardinality categoricals
                ml.Transforms.Categorical.OneHotEncoding("CountryOneHot", nameof(TransactionTrainingRow.Country))
                .Append(ml.Transforms.Categorical.OneHotEncoding("CurrencyOneHot", nameof(TransactionTrainingRow.Currency)))
                .Append(ml.Transforms.Categorical.OneHotEncoding("TxnTypeOneHot", nameof(TransactionTrainingRow.TransactionType)))
                .Append(ml.Transforms.Categorical.OneHotEncoding("ChannelOneHot", nameof(TransactionTrainingRow.Channel)))
                .Append(ml.Transforms.Categorical.OneHotEncoding("MccOneHot", nameof(TransactionTrainingRow.MerchantCategory)))
                .Append(ml.Transforms.Categorical.OneHotEncoding("DeviceTypeOneHot", nameof(TransactionTrainingRow.DeviceType)))

                // High-cardinality strings (IDs/tokens). Hashing limits feature explosion.
                .Append(ml.Transforms.Categorical.OneHotHashEncoding(
                    outputColumnName: "MerchantHash",
                    inputColumnName: nameof(TransactionTrainingRow.MerchantId),
                    numberOfBits: 14))

                .Append(ml.Transforms.Categorical.OneHotHashEncoding(
                    outputColumnName: "CustomerHash",
                    inputColumnName: nameof(TransactionTrainingRow.CustomerId),
                    numberOfBits: 15))

                .Append(ml.Transforms.Categorical.OneHotHashEncoding(
                    outputColumnName: "DeviceHash",
                    inputColumnName: nameof(TransactionTrainingRow.DeviceId),
                    numberOfBits: 14))

                .Append(ml.Transforms.Categorical.OneHotHashEncoding(
                    outputColumnName: "PaymentHash",
                    inputColumnName: nameof(TransactionTrainingRow.PaymentMethodToken),
                    numberOfBits: 15))

                // Bool -> float (0/1) so all model inputs are numeric
                .Append(ml.Transforms.Conversion.ConvertType("IsNewDeviceF", nameof(TransactionTrainingRow.IsNewDevice), DataKind.Single))
                .Append(ml.Transforms.Conversion.ConvertType("IsNewTokenF", nameof(TransactionTrainingRow.IsNewPaymentToken), DataKind.Single))
                .Append(ml.Transforms.Conversion.ConvertType("IsInternationalF", nameof(TransactionTrainingRow.IsInternational), DataKind.Single))

                // Normalize numeric features
                .Append(ml.Transforms.NormalizeMeanVariance("AmountNorm", nameof(TransactionTrainingRow.Amount)))
                .Append(ml.Transforms.NormalizeMeanVariance("HourNorm", nameof(TransactionTrainingRow.HourOfDay)))
                .Append(ml.Transforms.NormalizeMeanVariance("DayNorm", nameof(TransactionTrainingRow.DayOfWeek)))
                .Append(ml.Transforms.NormalizeMeanVariance("AcctAgeNorm", nameof(TransactionTrainingRow.AccountAgeDays)))
                .Append(ml.Transforms.NormalizeMeanVariance("CustAgeNorm", nameof(TransactionTrainingRow.CustomerAge)))
                .Append(ml.Transforms.NormalizeMeanVariance("Txn1hNorm", nameof(TransactionTrainingRow.TxnCountLast1h)))
                .Append(ml.Transforms.NormalizeMeanVariance("Txn24hNorm", nameof(TransactionTrainingRow.TxnCountLast24h)))
                .Append(ml.Transforms.NormalizeMeanVariance("Amt24hNorm", nameof(TransactionTrainingRow.TotalAmountLast24h)))
                .Append(ml.Transforms.NormalizeMeanVariance("PmAgeNorm", nameof(TransactionTrainingRow.PaymentMethodAgeDays)))
                .Append(ml.Transforms.NormalizeMeanVariance("DistNorm", nameof(TransactionTrainingRow.DistanceFromHomeKm)))
                .Append(ml.Transforms.NormalizeMeanVariance("MccRiskNorm", nameof(TransactionTrainingRow.MccRisk)))

                // Final Features vector
                .Append(ml.Transforms.Concatenate("Features",
                    "CountryOneHot", "CurrencyOneHot", "TxnTypeOneHot", "ChannelOneHot", "MccOneHot", "DeviceTypeOneHot",
                    "MerchantHash", "CustomerHash", "DeviceHash", "PaymentHash",
                    "IsNewDeviceF", "IsNewTokenF", "IsInternationalF",
                    "AmountNorm", "HourNorm", "DayNorm", "AcctAgeNorm", "CustAgeNorm",
                    "Txn1hNorm", "Txn24hNorm", "Amt24hNorm", "PmAgeNorm", "DistNorm", "MccRiskNorm"
                ))
                .AppendCacheCheckpoint(ml);

            // ------------------------------------------------------------
            // Trainer
            //
            // LightGBM performs well on tabular data and is a strong choice
            // for real-time scoring when paired with feature engineering.
            // ------------------------------------------------------------

            var trainer = ml.BinaryClassification.Trainers.LightGbm(
                labelColumnName: nameof(TransactionTrainingRow.Label),
                featureColumnName: "Features",
                numberOfIterations: 1200,
                learningRate: 0.05,
                numberOfLeaves: 64,
                minimumExampleCountPerLeaf: 40);

            var trainingPipeline = pipeline.Append(trainer);

            Console.WriteLine("Training model...");
            ITransformer model;
            using (new ConsoleSpinner("Training LightGBM"))
            {
                model = trainingPipeline.Fit(trainData);
            }
            Console.WriteLine("Training complete.");

            Console.WriteLine("Evaluating...");
            var scored = model.Transform(testData);

            var metrics = ml.BinaryClassification.Evaluate(scored, labelColumnName: nameof(TransactionTrainingRow.Label));
            PrintStandardMetrics(metrics);

            // Alert-rate evaluation approximates a real fraud operations workflow:
            // “We can review N alerts/day, so how good are the top N predictions?”
            EvaluateTopK(scored, ml, alertRate: 0.005);

            Console.WriteLine($"Saving model: {modelPath}");
            ml.Model.Save(model, trainData.Schema, modelPath);

            var info = new
            {
                CreatedUtc = DateTimeOffset.UtcNow,
                Trainer = "LightGbmBinary",
                AlertRate = "0.5%",
                Metrics = new
                {
                    metrics.Accuracy,
                    metrics.AreaUnderRocCurve,
                    metrics.AreaUnderPrecisionRecallCurve,
                    metrics.F1Score,
                    metrics.PositivePrecision,
                    metrics.PositiveRecall
                }
            };

            File.WriteAllText(infoPath, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Saved metadata: {infoPath}");
        }

        private static void PrintLabelDistribution(MLContext ml, IDataView data, string name)
        {
            long total = 0;
            long fraud = 0;

            foreach (var r in ml.Data.CreateEnumerable<TransactionTrainingRow>(data, reuseRowObject: false))
            {
                total++;
                if (r.Label) fraud++;
            }

            Console.WriteLine();
            Console.WriteLine($"[{name}] Total={total}, Fraud={fraud}, FraudRate={(total == 0 ? 0 : fraud / (double)total):P4}");
            Console.WriteLine();
        }

        private static void PrintStandardMetrics(BinaryClassificationMetrics metrics)
        {
            Console.WriteLine();
            Console.WriteLine("==== Metrics (Test) ====");
            Console.WriteLine($"Accuracy:  {metrics.Accuracy:P2}");
            Console.WriteLine($"ROC-AUC:   {metrics.AreaUnderRocCurve:P2}");
            Console.WriteLine($"PR-AUC:    {metrics.AreaUnderPrecisionRecallCurve:P2}");
            Console.WriteLine($"F1:        {metrics.F1Score:P2}");
            Console.WriteLine($"Precision: {metrics.PositivePrecision:P2}");
            Console.WriteLine($"Recall:    {metrics.PositiveRecall:P2}");
            Console.WriteLine("========================");
            Console.WriteLine();
        }

        private static void EvaluateTopK(IDataView scored, MLContext ml, double alertRate)
        {
            var rows = ml.Data.CreateEnumerable<ScoreRow>(scored, reuseRowObject: false).ToList();
            if (rows.Count == 0) return;

            var k = (int)Math.Max(1, Math.Round(rows.Count * alertRate));

            var topK = rows
                .OrderByDescending(r => r.Probability)
                .Take(k)
                .ToList();

            var totalFraud = rows.Count(r => r.Label);
            var tp = topK.Count(r => r.Label);
            var fp = k - tp;
            var fn = totalFraud - tp;

            var precision = k == 0 ? 0 : tp / (double)k;
            var recall = totalFraud == 0 ? 0 : tp / (double)totalFraud;

            Console.WriteLine($"==== Top-{alertRate:P2} alerting ====");
            Console.WriteLine($"k={k}  TP={tp} FP={fp} FN={fn}  Precision={precision:P2} Recall={recall:P2}");
            Console.WriteLine();
        }

        private static string FindProjectDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var csproj = Path.Combine(dir.FullName, "FraudModelTrainer.OptionA.csproj");
                if (File.Exists(csproj))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return Directory.GetCurrentDirectory();
        }

        private sealed class ConsoleSpinner : IDisposable
        {
            private readonly string _message;
            private readonly System.Threading.CancellationTokenSource _cts = new();
            private readonly System.Threading.Tasks.Task _task;

            public ConsoleSpinner(string message)
            {
                _message = message;
                _task = System.Threading.Tasks.Task.Run(Spin);
            }

            private async System.Threading.Tasks.Task Spin()
            {
                var frames = new[] { '|', '/', '-', '\\' };
                var i = 0;
                var start = DateTime.UtcNow;

                while (!_cts.IsCancellationRequested)
                {
                    var elapsed = DateTime.UtcNow - start;
                    Console.Write($"\r{_message} {frames[i++ % frames.Length]}  elapsed {elapsed:hh\\:mm\\:ss}");
                    await System.Threading.Tasks.Task.Delay(120);
                }
            }

            public void Dispose()
            {
                _cts.Cancel();
                try { _task.Wait(500); } catch { /* ignore */ }
                Console.WriteLine();
            }
        }
    }
}