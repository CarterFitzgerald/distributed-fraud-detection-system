namespace FraudDetectionWorker.Messaging
{
    /// <summary>
    /// Configuration options for connecting to RabbitMQ.
    /// Bound from the `RabbitMq` section in appsettings.json.
    /// </summary>
    public sealed class RabbitMqOptions
    {
        public string Host { get; set; } = "rabbitmq";
        public int Port { get; set; } = 5672;
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string QueueName { get; set; } = "transactions.created";
    }
}