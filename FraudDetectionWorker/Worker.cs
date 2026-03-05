using FraudDetectionWorker.Application;
using FraudDetectionWorker.Messaging;

namespace FraudDetectionWorker
{
    /// <summary>
    /// Background service responsible for consuming transaction events
    /// and delegating processing to the fraud detection pipeline.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IMessageConsumer _consumer;
        private readonly TransactionCreatedHandler _handler;

        public Worker(
            ILogger<Worker> logger,
            IMessageConsumer consumer,
            TransactionCreatedHandler handler)
        {
            _logger = logger;
            _consumer = consumer;
            _handler = handler;
        }

        /// <summary>
        /// Starts the worker lifecycle.
        /// </summary>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting FraudDetectionWorker...");
            await base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Starts the message consumer and processes messages indefinitely
        /// until the host shuts down.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running. Waiting for transaction events...");

            await _consumer.StartAsync(
                onMessageAsync: (payload, ct) => _handler.HandleAsync(payload, ct),
                ct: stoppingToken);

            // Keep the service alive while the consumer runs
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        /// <summary>
        /// Gracefully stops message consumption.
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping FraudDetectionWorker...");
            await _consumer.StopAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}