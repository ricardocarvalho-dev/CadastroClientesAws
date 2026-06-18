using CadastroClientes.Application.Interfaces;
using CadastroClientes.Application.UseCases;
using CadastroClientes.Infrastructure.Data;
using CadastroClientes.Infrastructure.Email;
using CadastroClientes.Infrastructure.Repositories;
using CadastroClientes.Worker;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Banco de dados (Azure SQL) — mesma connection string usada na API
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});

// RabbitMQ — mesma URI amqps:// usada na API
var rabbitMqUri = builder.Configuration["RabbitMQ:Uri"] ?? "amqp://guest:guest@localhost:5672/";
var connectionFactory = new ConnectionFactory
{
    Uri = new Uri(rabbitMqUri),
    DispatchConsumersAsync = true,
    Ssl = new SslOption
    {
        Enabled = rabbitMqUri.StartsWith("amqps"),
        AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch |
                                 System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
    }
};
builder.Services.AddSingleton<IConnectionFactory>(connectionFactory);

// E-mail via Resend
builder.Services.AddHttpClient<IEmailService, ResendEmailService>();

// Repositórios e Use Cases
builder.Services.AddScoped<IHistoricoEnvioMensagemRepository, HistoricoEnvioMensagemRepository>();
builder.Services.AddScoped<ProcessarEnvioEmailUseCase>();

// Worker que consome a fila do RabbitMQ
builder.Services.AddHostedService<RabbitMqConsumerWorker>();

var host = builder.Build();
host.Run();