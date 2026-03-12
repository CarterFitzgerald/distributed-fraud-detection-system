using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FraudDetectionWorker.Messaging
{
    /// <summary>
    /// RabbitMQ message consumer that:
    /// - declares the queue (idempotent)
    /// - consumes messages with manual acknowledgements
    /// - invokes a handler callback with the raw JSON payload
    /// </summary>
    public sealed class RabbitMqMessageConsumer : IMessageConsumer
    {
        private readonly RabbitMqOptions _options;

        private IConnection? _connection;
        private IChannel? _channel;

        public RabbitMqMessageConsumer(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;
        }

        public async Task StartAsync(Func<string, CancellationToken, Task> onMessageAsync, CancellationToken ct)
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password
            };

            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync();

            await _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            // Prefetch=1 ensures the consumer processes one message at a time and only receives the next
            // after acknowledging the current one (helps with backpressure and avoids burst memory usage).
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: ct);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                if (_channel is null) return;

                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    await onMessageAsync(json, ct);

                    // Acknowledge only after successful processing.
                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: ct);
                }
                catch
                {
                    // Negative-ack with requeue keeps the message durable in the presence of transient failures.
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
                }
            };

            await _channel.BasicConsumeAsync(queue: _options.QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);
        }

        public async Task StopAsync(CancellationToken ct)
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync(ct);
                await _channel.DisposeAsync();
            }

            _connection?.Dispose();
        }
    }
}