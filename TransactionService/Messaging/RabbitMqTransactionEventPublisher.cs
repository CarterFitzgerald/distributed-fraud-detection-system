using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using DistributedFraud.Contracts.Events;
using TransactionService.Models;

namespace TransactionService.Messaging
{
    /// <summary>
    /// RabbitMQ implementation of <see cref="ITransactionEventPublisher"/>.
    /// Serializes events as JSON and sends them to a configured queue.
    /// </summary>
    public class RabbitMqTransactionEventPublisher : ITransactionEventPublisher
    {
        private readonly RabbitMqOptions _options;

        public RabbitMqTransactionEventPublisher(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;
        }

        public async Task PublishTransactionCreatedAsync(Transaction transaction)
        {
            // Map domain entity to event DTO.
            var evt = new TransactionCreatedEvent
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                MerchantId = transaction.MerchantId,
                CustomerId = transaction.CustomerId,
                Country = transaction.Country,
                Timestamp = transaction.Timestamp
            };

            // Serialize event as JSON.
            var payload = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(payload);

            // Create connection + channel for this publish.
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            // Ensure the queue exists (idempotent).
            await channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var props = new BasicProperties
            {
                ContentType = "application/json",
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: _options.QueueName,
                mandatory: false,
                basicProperties: props,
                body: body);
        }
    }
}