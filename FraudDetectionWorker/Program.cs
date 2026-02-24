using FraudDetectionWorker;
using FraudDetectionWorker.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// Bind RabbitMQ options from appsettings.json
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();