using FraudDetectionWorker.Application;
using FraudDetectionWorker.Messaging;

namespace FraudDetectionWorker
{
    /// <summary>
    /// Hosted service that starts the message consumer and delegates processing
    /// to the TransactionCreatedHandler.
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

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting FraudDetectionWorker...");
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker is running. Starting message consumer...");

            await _consumer.StartAsync(
                onMessageAsync: (payload, ct) => _handler.HandleAsync(payload, ct),
                ct: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping FraudDetectionWorker...");
            await _consumer.StopAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}