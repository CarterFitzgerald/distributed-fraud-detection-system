using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using FraudDetectionWorker.Messaging;

namespace FraudDetectionWorker
{
    /// <summary>
    /// Background worker that subscribes to TransactionCreated events from RabbitMQ.
    /// For Day 8 the worker simply logs received events.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMqOptions _options;

        // RabbitMQ resources we keep open for the lifetime of the worker.
        private IConnection? _connection;
        private IChannel? _channel;

        public Worker(ILogger<Worker> logger, IOptions<RabbitMqOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting FraudDetectionWorker...");

            // Create RabbitMQ connection and channel.
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync();

            // Ensure the queue exists (idempotent). Must match publisher queue.
            await _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            // Optional: fairness - only dispatch one unacked message at a time.
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel is null)
            {
                _logger.LogError("RabbitMQ channel was not initialized.");
                return;
            }

            _logger.LogInformation("Listening for messages on queue: {Queue}", _options.QueueName);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation("Received TransactionCreated event: {Message}", message);

                    // ✅ Acknowledge after successful processing
                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from RabbitMQ.");

                    // ❌ Reject and requeue for now (simple + safe)
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                }
            };

            // Start consuming (autoAck=false because we manually ack/nack).
            await _channel.BasicConsumeAsync(
                queue: _options.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            // Keep the background service alive until cancellation is requested.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping FraudDetectionWorker...");

            if (_channel is not null)
            {
                await _channel.CloseAsync(cancellationToken);
                await _channel.DisposeAsync();
            }

            _connection?.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}