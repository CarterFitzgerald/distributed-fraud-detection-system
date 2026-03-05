using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FraudModelTrainer
{
    /// <summary>
    /// Demonstration trainer using the public Kaggle credit card fraud dataset (creditcard.csv).
    ///
    /// Purpose:
    /// - Show an end-to-end ML.NET training workflow on real imbalanced fraud data.
    /// - Persist a trained model (model.zip) and a metadata summary (model_info.json).
    ///
    /// Dataset schema (Kaggle):
    /// - Time, V1..V28, Amount, Class
    /// - Class: 1 = Fraud, 0 = Legit
    ///
    /// Notes:
    /// - This program uses a stratified split to preserve fraud/legit distribution.
    /// - Evaluation includes standard ML.NET metrics and alert-rate (top-k) analysis.
    /// </summary>
    public static class Program
    {
        // ------------------------------------------------------------
        // Data contracts
        // ------------------------------------------------------------

        /// <summary>
        /// Raw Kaggle row contract. Column indices match creditcard.csv.
        /// </summary>
        private sealed class CreditCardRow
        {
            [LoadColumn(0)] public float Time { get; set; }

            [LoadColumn(1)] public float V1 { get; set; }
            [LoadColumn(2)] public float V2 { get; set; }
            [LoadColumn(3)] public float V3 { get; set; }
            [LoadColumn(4)] public float V4 { get; set; }
            [LoadColumn(5)] public float V5 { get; set; }
            [LoadColumn(6)] public float V6 { get; set; }
            [LoadColumn(7)] public float V7 { get; set; }
            [LoadColumn(8)] public float V8 { get; set; }
            [LoadColumn(9)] public float V9 { get; set; }
            [LoadColumn(10)] public float V10 { get; set; }
            [LoadColumn(11)] public float V11 { get; set; }
            [LoadColumn(12)] public float V12 { get; set; }
            [LoadColumn(13)] public float V13 { get; set; }
            [LoadColumn(14)] public float V14 { get; set; }
            [LoadColumn(15)] public float V15 { get; set; }
            [LoadColumn(16)] public float V16 { get; set; }
            [LoadColumn(17)] public float V17 { get; set; }
            [LoadColumn(18)] public float V18 { get; set; }
            [LoadColumn(19)] public float V19 { get; set; }
            [LoadColumn(20)] public float V20 { get; set; }
            [LoadColumn(21)] public float V21 { get; set; }
            [LoadColumn(22)] public float V22 { get; set; }
            [LoadColumn(23)] public float V23 { get; set; }
            [LoadColumn(24)] public float V24 { get; set; }
            [LoadColumn(25)] public float V25 { get; set; }
            [LoadColumn(26)] public float V26 { get; set; }
            [LoadColumn(27)] public float V27 { get; set; }
            [LoadColumn(28)] public float V28 { get; set; }

            [LoadColumn(29)] public float Amount { get; set; }

            // Kaggle label: 0 (legit) / 1 (fraud). Kept numeric here; converted to bool later.
            [LoadColumn(30)] public float Class { get; set; }
        }

        /// <summary>
        /// Output schema from the custom mapping transform:
        /// - Label: bool label for ML.NET binary classifiers
        /// - Weight: example weight to address class imbalance
        /// </summary>
        private sealed class LabelWeightMapping
        {
            public bool Label { get; set; }
            public float Weight { get; set; }
        }

        /// <summary>
        /// Scoring projection used for alert-rate evaluation (top-k by predicted probability).
        /// </summary>
        private sealed class ScoredForEval
        {
            public bool Label { get; set; }
            public float Probability { get; set; }
            public float Score { get; set; }

            [ColumnName("PredictedLabel")]
            public bool PredictedLabel { get; set; }
        }

        public static void Main(string[] args)
        {
            var ml = new MLContext(seed: 42);

            // ------------------------------------------------------------
            // Paths and basic validation
            // ------------------------------------------------------------

            var projectDir = FindProjectDirectory();
            var dataDir = Path.Combine(projectDir, "Data");
            var dataPath = Path.Combine(dataDir, "creditcard.csv");

            var modelDir = Path.Combine(projectDir, "Model");
            var modelPath = Path.Combine(modelDir, "model.zip");
            var modelInfoPath = Path.Combine(modelDir, "model_info.json");
            Directory.CreateDirectory(modelDir);

            if (!File.Exists(dataPath))
            {
                Console.WriteLine($"❌ Could not find dataset: {dataPath}");
                Console.WriteLine("Place Kaggle creditcard.csv in FraudModelTrainer/Data/creditcard.csv");
                Environment.Exit(1);
            }

            Console.WriteLine($"Loading dataset: {dataPath}");

            // Kaggle file includes a header row.
            var fullData = ml.Data.LoadFromTextFile<CreditCardRow>(
                path: dataPath,
                hasHeader: true,
                separatorChar: ',',
                allowQuoting: true,
                trimWhitespace: true);

            PrintLabelDistribution(ml, fullData, name: "FULL");

            // ------------------------------------------------------------
            // Stratified split
            //
            // Fraud detection is heavily imbalanced; a naive random split can
            // produce unstable fraud counts and misleading evaluation.
            //
            // ML.NET's TrainTestSplit is not stratified, so we split fraud and
            // legit rows independently and then recombine.
            // ------------------------------------------------------------

            var fraudOnly = ml.Data.FilterRowsByColumn(fullData, nameof(CreditCardRow.Class), lowerBound: 1, upperBound: 2);
            var legitOnly = ml.Data.FilterRowsByColumn(fullData, nameof(CreditCardRow.Class), lowerBound: 0, upperBound: 1);

            var fraudSplit = ml.Data.TrainTestSplit(fraudOnly, testFraction: 0.2, seed: 42);
            var legitSplit = ml.Data.TrainTestSplit(legitOnly, testFraction: 0.2, seed: 42);

            var train = ConcatDataViews(ml, fraudSplit.TrainSet, legitSplit.TrainSet);
            var test = ConcatDataViews(ml, fraudSplit.TestSet, legitSplit.TestSet);

            PrintLabelDistribution(ml, train, name: "TRAIN");
            PrintLabelDistribution(ml, test, name: "TEST");

            // ------------------------------------------------------------
            // Pipeline
            //
            // 1) Custom mapping converts numeric Class -> bool Label.
            // 2) Assign example weights to counter class imbalance.
            // 3) Concatenate numeric features into a single Features vector.
            // 4) Normalize for better training stability.
            // ------------------------------------------------------------

            var toLabelAndWeight =
                ml.Transforms.CustomMapping<CreditCardRow, LabelWeightMapping>(
                    mapAction: (input, output) =>
                    {
                        var isFraud = input.Class >= 0.5f;
                        output.Label = isFraud;

                        // Weighting: amplify fraud examples.
                        // This is a practical approach for highly imbalanced datasets.
                        // The value (e.g., 600) can be tuned based on desired recall/precision tradeoffs.
                        output.Weight = isFraud ? 600f : 1f;
                    },
                    contractName: "ClassToLabelAndWeight");

            var featureColumns = new[]
            {
                nameof(CreditCardRow.Time),
                nameof(CreditCardRow.Amount),

                nameof(CreditCardRow.V1), nameof(CreditCardRow.V2), nameof(CreditCardRow.V3), nameof(CreditCardRow.V4),
                nameof(CreditCardRow.V5), nameof(CreditCardRow.V6), nameof(CreditCardRow.V7), nameof(CreditCardRow.V8),
                nameof(CreditCardRow.V9), nameof(CreditCardRow.V10), nameof(CreditCardRow.V11), nameof(CreditCardRow.V12),
                nameof(CreditCardRow.V13), nameof(CreditCardRow.V14), nameof(CreditCardRow.V15), nameof(CreditCardRow.V16),
                nameof(CreditCardRow.V17), nameof(CreditCardRow.V18), nameof(CreditCardRow.V19), nameof(CreditCardRow.V20),
                nameof(CreditCardRow.V21), nameof(CreditCardRow.V22), nameof(CreditCardRow.V23), nameof(CreditCardRow.V24),
                nameof(CreditCardRow.V25), nameof(CreditCardRow.V26), nameof(CreditCardRow.V27), nameof(CreditCardRow.V28),
            };

            IEstimator<ITransformer> pipeline =
                toLabelAndWeight
                    .Append(ml.Transforms.Concatenate("Features", featureColumns))
                    .Append(ml.Transforms.NormalizeMeanVariance("Features"))
                    .AppendCacheCheckpoint(ml);

            // FastTree is a strong baseline for tabular binary classification.
            // It trains quickly and often performs well without extensive feature work.
            var trainer = ml.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                exampleWeightColumnName: "Weight",
                numberOfTrees: 500,
                numberOfLeaves: 64,
                minimumExampleCountPerLeaf: 20,
                learningRate: 0.2);

            var trainingPipeline = pipeline.Append(trainer);

            Console.WriteLine("Training model (FastTree)...");
            var model = trainingPipeline.Fit(train);

            Console.WriteLine("Evaluating model...");
            var scored = model.Transform(test);

            // Standard ML metrics are useful, but for fraud the alert-rate view is often more actionable.
            var metrics = ml.BinaryClassification.Evaluate(scored, labelColumnName: "Label");
            PrintStandardMetrics(metrics);

            var evalRows = ml.Data.CreateEnumerable<ScoredForEval>(scored, reuseRowObject: false);
            EvaluateAtAlertRate(evalRows, alertRate: 0.005); // top 0.5% flagged
            EvaluateAtAlertRate(evalRows, alertRate: 0.002); // top 0.2% flagged
            EvaluateAtAlertRate(evalRows, alertRate: 0.001); // top 0.1% flagged

            Console.WriteLine($"Saving model: {modelPath}");
            ml.Model.Save(model, train.Schema, modelPath);

            var info = new
            {
                Dataset = "Kaggle creditcard.csv (Time, V1..V28, Amount, Class)",
                Label = "Class (1=fraud, 0=legit)",
                CreatedUtc = DateTimeOffset.UtcNow,
                Trainer = "FastTree (Gradient Boosted Trees)",
                Metrics = new
                {
                    metrics.Accuracy,
                    metrics.AreaUnderRocCurve,
                    metrics.AreaUnderPrecisionRecallCurve,
                    metrics.F1Score,
                    metrics.PositivePrecision,
                    metrics.PositiveRecall
                },
                Notes = new[]
                {
                    "Fraud is highly imbalanced; PR-AUC and top-k alerting are more meaningful than accuracy.",
                    "Train/test split is stratified by label (fraud/legit split separately).",
                    "Example weighting is used to reduce false negatives (increase fraud recall)."
                }
            };

            File.WriteAllText(modelInfoPath, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Saved model metadata: {modelInfoPath}");
            Console.WriteLine("Done.");
        }

        private static IDataView ConcatDataViews(MLContext ml, IDataView a, IDataView b)
        {
            // This approach is simple and sufficient for the Kaggle dataset size (~284k rows).
            // For very large datasets, a more streaming-friendly approach would be preferred.
            var ea = ml.Data.CreateEnumerable<CreditCardRow>(a, reuseRowObject: false);
            var eb = ml.Data.CreateEnumerable<CreditCardRow>(b, reuseRowObject: false);
            return ml.Data.LoadFromEnumerable(ea.Concat(eb));
        }

        private static void PrintLabelDistribution(MLContext ml, IDataView data, string name)
        {
            long total = 0;
            long fraud = 0;

            foreach (var row in ml.Data.CreateEnumerable<CreditCardRow>(data, reuseRowObject: false))
            {
                total++;
                if (row.Class >= 0.5f)
                    fraud++;
            }

            var legit = total - fraud;

            Console.WriteLine();
            Console.WriteLine($"[{name}] Label distribution:");
            Console.WriteLine($"  Total: {total}");
            Console.WriteLine($"  Legit: {legit}");
            Console.WriteLine($"  Fraud: {fraud}");
            Console.WriteLine($"  Fraud rate: {(total == 0 ? 0 : fraud / (double)total):P4}");
            Console.WriteLine();
        }

        private static void PrintStandardMetrics(BinaryClassificationMetrics metrics)
        {
            Console.WriteLine();
            Console.WriteLine("==== ML.NET Metrics ====");
            Console.WriteLine($"Accuracy:  {metrics.Accuracy:P2}");
            Console.WriteLine($"ROC-AUC:   {metrics.AreaUnderRocCurve:P2}");
            Console.WriteLine($"PR-AUC:    {metrics.AreaUnderPrecisionRecallCurve:P2}");
            Console.WriteLine($"F1:        {metrics.F1Score:P2}");
            Console.WriteLine($"Precision: {metrics.PositivePrecision:P2}");
            Console.WriteLine($"Recall:    {metrics.PositiveRecall:P2}");
            Console.WriteLine("========================");
            Console.WriteLine();
        }

        private static void EvaluateAtAlertRate(IEnumerable<ScoredForEval> rows, double alertRate)
        {
            var list = rows.ToList();
            if (list.Count == 0) return;

            // Fraud systems are commonly tuned by operational alert capacity.
            // This evaluates how many true frauds appear in the top-k predictions.
            var k = (int)Math.Max(1, Math.Round(list.Count * alertRate));

            var topK = list
                .OrderByDescending(r => r.Probability)
                .Take(k)
                .ToList();

            var cutoffProb = topK.Last().Probability;

            var totalFraud = list.Count(r => r.Label);
            var tp = topK.Count(r => r.Label);
            var fp = k - tp;
            var fn = totalFraud - tp;

            var precision = k == 0 ? 0 : tp / (double)k;
            var recall = totalFraud == 0 ? 0 : tp / (double)totalFraud;

            Console.WriteLine($"==== Alert-rate eval (top {alertRate:P2}) ====");
            Console.WriteLine($"k={k} cutoffProb={cutoffProb:F6}");
            Console.WriteLine($"TP={tp} FP={fp} FN={fn} (TotalFraud={totalFraud})");
            Console.WriteLine($"Precision={precision:P2} Recall={recall:P2}");

            var avgFraudProb = list.Where(r => r.Label).Select(r => r.Probability).DefaultIfEmpty().Average();
            var avgLegitProb = list.Where(r => !r.Label).Select(r => r.Probability).DefaultIfEmpty().Average();
            Console.WriteLine($"AvgProb fraud={avgFraudProb:F6} legit={avgLegitProb:F6}");
            Console.WriteLine();
        }

        private static string FindProjectDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var csproj = Path.Combine(dir.FullName, "FraudModelTrainer.csproj");
                if (File.Exists(csproj))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }
}