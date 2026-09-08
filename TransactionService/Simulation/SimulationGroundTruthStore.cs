using System.Collections.Concurrent;

namespace TransactionService.Simulation
{
    /// <summary>
    /// In-memory record of the labels the simulator assigned when generating
    /// transactions. This is never seen by the ML model or FraudDetectionWorker —
    /// it exists purely so the accuracy report can compare intent vs. prediction.
    ///
    /// Registered as a singleton so it survives across the scoped requests that
    /// make up one simulation run. Resets at the start of each new run.
    /// </summary>
    public interface ISimulationGroundTruthStore
    {
        void Reset();
        void Record(Guid transactionId, bool expectedFraud, string scenario);
        IReadOnlyDictionary<Guid, (bool ExpectedFraud, string Scenario)> Snapshot();
    }

    public sealed class SimulationGroundTruthStore : ISimulationGroundTruthStore
    {
        private ConcurrentDictionary<Guid, (bool ExpectedFraud, string Scenario)> _labels = new();

        public void Reset() => _labels = new ConcurrentDictionary<Guid, (bool, string)>();

        public void Record(Guid transactionId, bool expectedFraud, string scenario)
            => _labels[transactionId] = (expectedFraud, scenario);

        public IReadOnlyDictionary<Guid, (bool ExpectedFraud, string Scenario)> Snapshot()
            => _labels;
    }
}