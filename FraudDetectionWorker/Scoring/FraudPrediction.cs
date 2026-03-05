using Microsoft.ML.Data;

namespace FraudDetectionWorker.Scoring
{
    /// <summary>
    /// ML.NET model output schema.
    /// </summary>
    public sealed class FraudPrediction
    {
        /// <summary>
        /// Predicted class label at the model's internal default threshold.
        /// Many fraud systems use probability ranking/thresholding instead.
        /// </summary>
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        /// <summary>Estimated probability that the transaction is fraudulent.</summary>
        public float Probability { get; set; }

        /// <summary>Raw model score (log-odds style signal depending on the trainer).</summary>
        public float Score { get; set; }
    }
}