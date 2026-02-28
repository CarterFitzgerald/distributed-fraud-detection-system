using FraudDetectionWorker;
using FraudDetectionWorker.Application;
using FraudDetectionWorker.Data;
using FraudDetectionWorker.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddDbContextFactory<TransactionDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(cs);
});

builder.Services.AddSingleton<IMessageConsumer, RabbitMqMessageConsumer>();
builder.Services.AddSingleton<TransactionCreatedHandler>();

builder.Services.AddHostedService<Worker>();

builder.Build().Run();