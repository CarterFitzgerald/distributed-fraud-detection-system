namespace FraudDetectionWorker.Messaging
{
    /// <summary>
    /// Configuration options for connecting to RabbitMQ.
    /// Bound from the `RabbitMq` section in appsettings.json.
    /// </summary>
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string QueueName { get; set; } = "transactions.created";
    }
}