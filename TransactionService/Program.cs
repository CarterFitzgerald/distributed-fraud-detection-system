using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<ITransactionStore, InMemoryTransactionStore>();
builder.Services.AddEndpointsApiExplorer();
object value = builder.Services.AddSwaggerGen();

//
// Database configuration
//
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Use SQL Server provider with the DefaultConnection string from configuration.
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

//
// Application services
//
// IMPORTANT: we're going to swap the implementation from in-memory to EF-backed
// in the next step, but the controller will still depend only on ITransactionStore.
builder.Services.AddScoped<ITransactionStore, EfTransactionStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();