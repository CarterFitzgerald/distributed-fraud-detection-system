using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML;

namespace FraudDetectionWorker.Scoring
{
    /// <summary>
    /// Loads an ML.NET model once at startup and provides thread-safe prediction.
    /// </summary>
    public sealed class FraudModelPredictor
    {
        private readonly MLContext _ml = new(seed: 42);
        private readonly ITransformer _model;

        // PredictionEngine is not thread-safe; ThreadLocal provides one engine per thread.
        private readonly ThreadLocal<PredictionEngine<TransactionFeatures, FraudPrediction>> _engine;

        public FraudModelPredictor(IConfiguration config, IHostEnvironment env, ILogger<FraudModelPredictor> logger)
        {
            var configuredPath = config.GetSection("FraudModel")["ModelPath"];
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new InvalidOperationException("FraudModel:ModelPath is not configured.");

            var resolvedPath = ResolveModelPath(configuredPath, env.ContentRootPath);

            if (!File.Exists(resolvedPath))
            {
                var found = TryFindModelUpwards(env.ContentRootPath);
                if (found is null)
                {
                    throw new FileNotFoundException(
                        "Model file not found.",
                        resolvedPath);
                }

                resolvedPath = found;
                logger.LogInformation("Model path fallback resolved to {Path}", resolvedPath);
            }

            logger.LogInformation("Loading model from {Path}", resolvedPath);

            using var fs = File.OpenRead(resolvedPath);
            _model = _ml.Model.Load(fs, out _);

            _engine = new ThreadLocal<PredictionEngine<TransactionFeatures, FraudPrediction>>(
                () => _ml.Model.CreatePredictionEngine<TransactionFeatures, FraudPrediction>(
                    _model,
                    ignoreMissingColumns: true),
                trackAllValues: false);
        }

        /// <summary>
        /// Runs inference for a single transaction.
        /// </summary>
        public FraudPrediction Predict(TransactionFeatures features)
        {
            if (features is null) throw new ArgumentNullException(nameof(features));

            var eng = _engine.Value ?? throw new InvalidOperationException("Prediction engine not initialized.");
            return eng.Predict(features);
        }

        private static string ResolveModelPath(string configuredPath, string contentRootPath)
        {
            // Supports absolute paths and paths relative to ContentRoot.
            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
        }

        private static string? TryFindModelUpwards(string startDir)
        {
            var dir = new DirectoryInfo(startDir);

            while (dir is not null)
            {
                var candidate = Path.Combine(
                    dir.FullName,
                    "FraudModelTrainer.OptionA",
                    "Model",
                    "model.zip");

                if (File.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }

            return null;
        }
    }
}