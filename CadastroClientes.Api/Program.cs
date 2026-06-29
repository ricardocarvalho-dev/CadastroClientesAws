using Amazon.Runtime;
using Amazon.SQS;
using CadastroClientes.Application.Interfaces;
using CadastroClientes.Application.UseCases;
using CadastroClientes.Infrastructure.Data;
using CadastroClientes.Infrastructure.Email;
using CadastroClientes.Infrastructure.Messaging;
using CadastroClientes.Infrastructure.Repositories;
using CadastroClientes.Infrastructure.Sms;
using CadastroClientes.Worker;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.CaptureStartupErrors(true).UseSetting("detailedErrors", "true");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Banco PostgreSQL (Supabase)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null));
});

// AWS SQS
var awsAccessKey = builder.Configuration["AWS:AccessKeyId"]
    ?? throw new InvalidOperationException("AWS:AccessKeyId não configurado.");
var awsSecretKey = builder.Configuration["AWS:SecretAccessKey"]
    ?? throw new InvalidOperationException("AWS:SecretAccessKey não configurado.");
var awsRegion = builder.Configuration["AWS:Region"] ?? "us-east-2";

var awsCredentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
var sqsClient = new AmazonSQSClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(awsRegion));
builder.Services.AddSingleton<IAmazonSQS>(sqsClient);

// Repositórios e Serviços
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IHistoricoEnvioMensagemRepository, HistoricoEnvioMensagemRepository>();
builder.Services.AddScoped<IMessagingService, SqsNotificationService>();

// Email e SMS para o Worker
builder.Services.AddHttpClient<IEmailService, ResendEmailService>();
builder.Services.AddSingleton<ISmsService, TwilioSmsService>();

// Use Cases
builder.Services.AddScoped<CriarClienteUseCase>();
builder.Services.AddScoped<ListarClientesUseCase>();
builder.Services.AddScoped<AtualizarClienteUseCase>();
builder.Services.AddScoped<ExcluirClienteUseCase>();
builder.Services.AddScoped<ProcessarEnvioEmailUseCase>();
builder.Services.AddScoped<ProcessarEnvioSmsUseCase>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Worker SQS rodando junto à API
builder.Services.AddHostedService<SqsConsumerWorker>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowBlazor");
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        Console.WriteLine(">>> Iniciando EF Core Migrate...");
        logger.LogInformation("Applying migrations...");
        db.Database.Migrate();
        Console.WriteLine(">>> EF Core Migrate concluído.");
        logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Erro ao aplicar migrations: {ex.Message}");
        logger.LogError(ex, "Error applying migrations");
    }
}

app.Run();