namespace FraudDetectionWorker.Scoring
{
    /// <summary>
    /// Configuration options for ML model loading and model versioning.
    /// </summary>
    public sealed class FraudModelOptions
    {
        /// <summary>
        /// Path to the serialized ML.NET model file.
        /// Can be absolute or relative to the service ContentRoot.
        /// </summary>
        public string ModelPath { get; set; } =
            @"\distributed-fraud-detection-system\FraudModelTrainer.OptionA\Model\model.zip";

        /// <summary>
        /// Logical model version persisted with scoring results (useful for audits and rollbacks).
        /// </summary>
        public string ModelVersion { get; set; } = "OptionA-LightGBM-v2";
    }
}