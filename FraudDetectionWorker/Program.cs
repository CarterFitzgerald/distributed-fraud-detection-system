using FraudDetectionWorker;
using FraudDetectionWorker.Application;
using FraudDetectionWorker.Data;
using FraudDetectionWorker.Features;
using FraudDetectionWorker.Messaging;
using FraudDetectionWorker.Scoring;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Entry point for the FraudDetectionWorker service.
/// 
/// Responsibilities:
/// • Configure dependency injection
/// • Configure infrastructure services (RabbitMQ, SQL Server)
/// • Register ML scoring pipeline components
/// • Start the hosted worker service
/// </summary>
var builder = Host.CreateApplicationBuilder(args);

// ---------------------------
// Configuration bindings
// ---------------------------

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.Configure<FraudModelOptions>(
    builder.Configuration.GetSection("FraudModel"));

// ---------------------------
// Database configuration
// ---------------------------

builder.Services.AddDbContextFactory<TransactionDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// ---------------------------
// Messaging infrastructure
// ---------------------------

builder.Services.AddSingleton<IMessageConsumer, RabbitMqMessageConsumer>();

// ---------------------------
// Fraud detection pipeline
// ---------------------------

// Loads and caches the ML.NET model for inference
builder.Services.AddSingleton<FraudModelPredictor>();

// Computes engineered transaction features
builder.Services.AddSingleton<TransactionFeatureComputer>();

// Message handler responsible for end-to-end scoring
builder.Services.AddSingleton<TransactionCreatedHandler>();

// ---------------------------
// Hosted background worker
// ---------------------------

builder.Services.AddHostedService<Worker>();

builder.Build().Run();