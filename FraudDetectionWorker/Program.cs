using FraudDetectionWorker;
using FraudDetectionWorker.Data;
using FraudDetectionWorker.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

// ✅ Register factory because Worker injects IDbContextFactory<TransactionDbContext>
builder.Services.AddDbContextFactory<TransactionDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(cs);
});

builder.Services.AddHostedService<Worker>();

builder.Build().Run();