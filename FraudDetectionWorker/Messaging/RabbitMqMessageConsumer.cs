using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FraudDetectionWorker.Messaging
{
    /// <summary>
    /// RabbitMQ implementation of IMessageConsumer.
    /// Connects to a queue and invokes a callback with the message payload.
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
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
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

            await _channel.BasicQosAsync(0, 1, false, ct);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                if (_channel is null) return;

                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    await onMessageAsync(json, ct);

                    await _channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                }
                catch
                {
                    // requeue on error (simple + safe)
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, ct);
                }
            };

            await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, ct);
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