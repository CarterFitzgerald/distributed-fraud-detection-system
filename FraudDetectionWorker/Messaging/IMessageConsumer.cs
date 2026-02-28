namespace FraudDetectionWorker.Messaging
{
    /// <summary>
    /// Abstraction for consuming messages from a broker.
    /// Keeps the Worker orchestration clean and testable.
    /// </summary>
    public interface IMessageConsumer
    {
        Task StartAsync(Func<string, CancellationToken, Task> onMessageAsync, CancellationToken ct);
        Task StopAsync(CancellationToken ct);
    }
}