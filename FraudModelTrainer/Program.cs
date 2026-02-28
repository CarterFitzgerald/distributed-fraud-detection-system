using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FraudModelTrainer
{
    public static class Program
    {
        // -----------------------------
        // Data contracts
        // -----------------------------

        /// <summary>
        /// Represents one row from the training CSV.
        /// Column names must match the header row in transactions_training.csv.
        /// </summary>
        private sealed class TransactionTrainingRow
        {
            [LoadColumn(0)]
            public float Amount { get; set; }

            [LoadColumn(1)]
            public string Country { get; set; } = string.Empty;

            [LoadColumn(2)]
            public string Currency { get; set; } = string.Empty;

            [LoadColumn(3)]
            public string MerchantId { get; set; } = string.Empty;

            [LoadColumn(4)]
            public string CustomerId { get; set; } = string.Empty;

            [LoadColumn(5)]
            public float HourOfDay { get; set; }

            [LoadColumn(6)]
            public bool IsFraud { get; set; }
        }

        /// <summary>
        /// Model prediction output.
        /// For binary classification, ML.NET provides:
        /// - PredictedLabel (bool)
        /// - Probability (0..1)
        /// - Score (raw model score)
        /// </summary>
        private sealed class FraudPrediction
        {
            [ColumnName("PredictedLabel")]
            public bool PredictedLabel { get; set; }

            public float Probability { get; set; }

            public float Score { get; set; }
        }

        public static void Main()
        {
            var ml = new MLContext(seed: 42);

            // Paths (relative to working directory). Using AppContext.BaseDirectory keeps it robust.
            var projectDir = FindProjectDirectory();
            var dataPath = Path.Combine(projectDir, "Data", "transactions_training.csv");
            var modelDir = Path.Combine(projectDir, "Model");
            var modelPath = Path.Combine(modelDir, "model.zip");
            var modelInfoPath = Path.Combine(modelDir, "model_info.json");

            Directory.CreateDirectory(modelDir);

            Console.WriteLine($"Loading training data: {dataPath}");

            // Load CSV into IDataView
            var data = ml.Data.LoadFromTextFile<TransactionTrainingRow>(
                path: dataPath,
                hasHeader: true,
                separatorChar: ',');

            // Split fraud + legit separately so the test set always contains fraud examples.
            var fraud = ml.Data.FilterRowsByColumn(data, nameof(TransactionTrainingRow.IsFraud), lowerBound: 1, upperBound: 1);
            var legit = ml.Data.FilterRowsByColumn(data, nameof(TransactionTrainingRow.IsFraud), lowerBound: 0, upperBound: 0);

            var fraudSplit = ml.Data.TrainTestSplit(fraud, testFraction: 0.2, seed: 42);
            var legitSplit = ml.Data.TrainTestSplit(legit, testFraction: 0.2, seed: 42);

            // Replace non-existent AppendRows by materializing partitions to IEnumerable<T> and reloading.
            var fraudTrainEnum = ml.Data.CreateEnumerable<TransactionTrainingRow>(fraudSplit.TrainSet, reuseRowObject: false);
            var legitTrainEnum = ml.Data.CreateEnumerable<TransactionTrainingRow>(legitSplit.TrainSet, reuseRowObject: false);
            var trainEnum = fraudTrainEnum.Concat(legitTrainEnum);
            var train = ml.Data.LoadFromEnumerable(trainEnum);

            var fraudTestEnum = ml.Data.CreateEnumerable<TransactionTrainingRow>(fraudSplit.TestSet, reuseRowObject: false);
            var legitTestEnum = ml.Data.CreateEnumerable<TransactionTrainingRow>(legitSplit.TestSet, reuseRowObject: false);
            var testEnum = fraudTestEnum.Concat(legitTestEnum);
            var test = ml.Data.LoadFromEnumerable(testEnum);

            // -----------------------------
            // Feature engineering pipeline
            // -----------------------------
            //
            // Why these steps?
            // - Country/Currency are low-cardinality categorical => OneHot
            // - MerchantId/CustomerId are high-cardinality => Hashing to avoid huge sparse vectors
            // - Amount/HourOfDay are numeric => Normalize
            //
            // Final feature vector: Features
            //
            IEstimator<ITransformer> pipeline =
                ml.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: nameof(TransactionTrainingRow.IsFraud));

            pipeline = pipeline
                .Append(ml.Transforms.Categorical.OneHotEncoding(
                    outputColumnName: "CountryOneHot",
                    inputColumnName: nameof(TransactionTrainingRow.Country)))

                .Append(ml.Transforms.Categorical.OneHotEncoding(
                    outputColumnName: "CurrencyOneHot",
                    inputColumnName: nameof(TransactionTrainingRow.Currency)))

                .Append(ml.Transforms.Conversion.Hash(
                    outputColumnName: "MerchantHash",
                    inputColumnName: nameof(TransactionTrainingRow.MerchantId),
                    numberOfBits: 14))

                .Append(ml.Transforms.Conversion.Hash(
                    outputColumnName: "CustomerHash",
                    inputColumnName: nameof(TransactionTrainingRow.CustomerId),
                    numberOfBits: 16))

                .Append(ml.Transforms.NormalizeMeanVariance(
                    outputColumnName: "AmountNorm",
                    inputColumnName: nameof(TransactionTrainingRow.Amount)))

                .Append(ml.Transforms.NormalizeMeanVariance(
                    outputColumnName: "HourNorm",
                    inputColumnName: nameof(TransactionTrainingRow.HourOfDay)))

                .Append(ml.Transforms.Concatenate(
                    outputColumnName: "Features",
                    "CountryOneHot",
                    "CurrencyOneHot",
                    "MerchantHash",
                    "CustomerHash",
                    "AmountNorm",
                    "HourNorm"))

                .AppendCacheCheckpoint(ml);

            // -----------------------------
            // Trainer
            // -----------------------------
            //
            // Start simple & strong:
            // - SDCA logistic regression is a solid baseline for binary classification.
            // - Outputs calibrated probabilities.
            //
            var trainer = ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label",
                featureColumnName: "Features");

            var trainingPipeline = pipeline.Append(trainer);

            Console.WriteLine("Training model...");
            var model = trainingPipeline.Fit(train);

            Console.WriteLine("Evaluating model...");
            var predictions = model.Transform(test);

            var metrics = ml.BinaryClassification.Evaluate(
                predictions, 
                labelColumnName: "Label");

            // Print metrics (portfolio-friendly)
            Console.WriteLine();
            Console.WriteLine("==== Metrics (Test Set) ====");
            Console.WriteLine($"Accuracy:  {metrics.Accuracy:P2}");
            Console.WriteLine($"AUC (ROC): {metrics.AreaUnderRocCurve:P2}");
            Console.WriteLine($"AUC (PR):  {metrics.AreaUnderPrecisionRecallCurve:P2}");
            Console.WriteLine($"F1 Score:  {metrics.F1Score:P2}");
            Console.WriteLine($"Precision: {metrics.PositivePrecision:P2}");
            Console.WriteLine($"Recall:    {metrics.PositiveRecall:P2}");
            Console.WriteLine();

            var cm = metrics.ConfusionMatrix;
            Console.WriteLine("Confusion Matrix:");
            Console.WriteLine($"  TN: {cm.Counts[0][0]}  FP: {cm.Counts[0][1]}");
            Console.WriteLine($"  FN: {cm.Counts[1][0]}  TP: {cm.Counts[1][1]}");
            Console.WriteLine("============================");

            // Save model
            Console.WriteLine($"Saving model: {modelPath}");
            ml.Model.Save(model, train.Schema, modelPath);

            // Write a small model info file (nice for versioning + traceability)
            var info = new
            {
                ModelPath = "Model/model.zip",
                CreatedUtc = DateTimeOffset.UtcNow,
                Trainer = "SdcaLogisticRegression",
                Features = new[]
                {
                "Country (OneHot)",
                "Currency (OneHot)",
                "MerchantId (HashedWordBags, 2^14)",
                "CustomerId (HashedWordBags, 2^16)",
                "Amount (Normalized)",
                "HourOfDay (Normalized)"
            },
                Metrics = new
                {
                    metrics.Accuracy,
                    metrics.AreaUnderRocCurve,
                    metrics.F1Score,
                    metrics.PositivePrecision,
                    metrics.PositiveRecall
                }
            };

            File.WriteAllText(modelInfoPath, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Saved model metadata: {modelInfoPath}");

            // Quick sanity inference example (optional)
            Console.WriteLine();
            Console.WriteLine("Running a quick sample prediction...");

            var engine = ml.Model.CreatePredictionEngine<TransactionTrainingRow, FraudPrediction>(model);

            var sample = new TransactionTrainingRow
            {
                Amount = 6000f,
                Country = "NG",
                Currency = "AUD",
                MerchantId = "m_0009",
                CustomerId = "c_07777",
                HourOfDay = 2f,
                IsFraud = false
            };

            var pred = engine.Predict(sample);

            Console.WriteLine($"Predicted Fraud? {pred.PredictedLabel} | Probability={pred.Probability:P2} | Score={pred.Score:F4}");
            Console.WriteLine();
            Console.WriteLine("Done.");
        }

        /// <summary>
        /// Finds the FraudModelTrainer project directory regardless of where the app is launched from.
        /// Assumes the executable runs from bin/... and walks up until it finds FraudModelTrainer.csproj.
        /// </summary>
        private static string FindProjectDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var csproj = Path.Combine(dir.FullName, "FraudModelTrainer.csproj");
                if (File.Exists(csproj))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            // Fallback: current working directory
            return Directory.GetCurrentDirectory();
        }
    }
}