using Amazon.SQS;
using Amazon.Runtime;
using CadastroClientes.Application.Interfaces;
using CadastroClientes.Application.UseCases;
using CadastroClientes.Infrastructure.Data;
using CadastroClientes.Infrastructure.Email;
using CadastroClientes.Infrastructure.Repositories;
using CadastroClientes.Infrastructure.Sms;
using CadastroClientes.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

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

// Email via Resend
builder.Services.AddHttpClient<IEmailService, ResendEmailService>();

// SMS via Twilio
builder.Services.AddSingleton<ISmsService, TwilioSmsService>();
builder.Services.AddScoped<ProcessarEnvioSmsUseCase>();

// Repositórios e Use Cases
builder.Services.AddScoped<IHistoricoEnvioMensagemRepository, HistoricoEnvioMensagemRepository>();
builder.Services.AddScoped<ProcessarEnvioEmailUseCase>();

// Worker SQS
builder.Services.AddHostedService<SqsConsumerWorker>();

var host = builder.Build();
host.Run();