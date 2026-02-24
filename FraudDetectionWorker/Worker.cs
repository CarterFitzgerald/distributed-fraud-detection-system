using FraudDetectionWorker.Data;
using FraudDetectionWorker.Messaging;
using FraudDetectionWorker.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using TransactionService.Events;

namespace FraudDetectionWorker
{
    /// <summary>
    /// Background worker that consumes TransactionCreated events from RabbitMQ,
    /// computes a fraud score (fake-first rule-based scoring), and persists the score
    /// back to the TransactionDb.
    ///
    /// Day 9 deliverable:
    /// POST (TransactionService) → RabbitMQ queue → Worker consumes → fraud score stored in DB.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMqOptions _options;
        private readonly IDbContextFactory<TransactionDbContext> _dbContextFactory;

        // RabbitMQ resources kept open for the lifetime of the worker.
        private IConnection? _connection;
        private IChannel? _channel;

        public Worker(
            ILogger<Worker> logger,
            IOptions<RabbitMqOptions> options,
            IDbContextFactory<TransactionDbContext> dbContextFactory)
        {
            _logger = logger;
            _options = options.Value;
            _dbContextFactory = dbContextFactory;
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
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: cancellationToken);

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
                // Always guard against nulls because the channel may be closing during shutdown.
                if (_channel is null)
                {
                    return;
                }

                try
                {
                    // 1) Read message payload
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    // 2) Deserialize into event contract
                    var evt = JsonSerializer.Deserialize<TransactionCreatedEvent>(json);

                    if (evt is null)
                    {
                        // If the message cannot be deserialized, ack it so it doesn't poison the queue.
                        _logger.LogWarning("Received invalid TransactionCreatedEvent (deserialization returned null). Payload: {Payload}", json);
                        await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        return;
                    }

                    // 3) Compute fake-first fraud score (0–100) + reason codes (explainable)
                    var (score, reason) = FraudScorer.Score(evt);

                    // 4) Persist scoring back to the TransactionDb
                    await using var db = await _dbContextFactory.CreateDbContextAsync(stoppingToken);

                    // Find the existing transaction row by Id
                    var tx = await db.Transactions.FirstOrDefaultAsync(t => t.Id == evt.Id, stoppingToken);

                    if (tx is null)
                    {
                        // The transaction might not have committed yet (rare), so we requeue for retry.
                        _logger.LogWarning(
                            "Transaction {TransactionId} not found in DB yet. Requeueing message for retry.",
                            evt.Id);

                        await _channel.BasicNackAsync(
                            deliveryTag: ea.DeliveryTag,
                            multiple: false,
                            requeue: true,
                            cancellationToken: stoppingToken);

                        return;
                    }

                    // Optional idempotency: if already scored, ack and skip.
                    if (tx.FraudScoredAt is not null)
                    {
                        _logger.LogInformation(
                            "Transaction {TransactionId} already scored (Score={Score}). Skipping.",
                            tx.Id,
                            tx.FraudScore);

                        await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        return;
                    }

                    tx.FraudScore = score;
                    tx.FraudReason = reason;
                    tx.FraudScoredAt = DateTimeOffset.UtcNow;

                    await db.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation(
                        "Scored transaction {TransactionId}: Score={Score} Reason={Reason}",
                        evt.Id,
                        score,
                        reason);

                    // ✅ Acknowledge after successful processing and persistence
                    await _channel.BasicAckAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from RabbitMQ. Message will be requeued.");

                    // ❌ Reject and requeue for retry (simple + safe for Day 9)
                    if (_channel is not null)
                    {
                        await _channel.BasicNackAsync(
                            deliveryTag: ea.DeliveryTag,
                            multiple: false,
                            requeue: true,
                            cancellationToken: stoppingToken);
                    }
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