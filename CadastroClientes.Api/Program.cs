using CadastroClientes.Application.Interfaces;
using CadastroClientes.Application.UseCases;
using CadastroClientes.Infrastructure.Data;
using CadastroClientes.Infrastructure.Messaging;
using CadastroClientes.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Habilita logging para console (capturado pelo Azure Log Stream)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.CaptureStartupErrors(true).UseSetting("detailedErrors", "true");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configura o Banco de Dados para usar SQLite com caminho persistente no Azure
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    }
    else
    {
        string dataPath = Path.Combine(AppContext.BaseDirectory, "data");
        if (!Directory.Exists(dataPath))
            Directory.CreateDirectory(dataPath);
        var dbPath = Path.Combine(dataPath, "cadastroclientes.db");
        SQLitePCL.Batteries.Init();
        options.UseSqlite($"Data Source={dbPath}");
    }
});

// Configura RabbitMQ
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

// Registro dos Repositórios e Serviços
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IHistoricoEnvioMensagemRepository, HistoricoEnvioMensagemRepository>();
builder.Services.AddScoped<IMessagingService, NotificationService>();

// Registro dos Use Cases
builder.Services.AddScoped<CriarClienteUseCase>();
builder.Services.AddScoped<ListarClientesUseCase>();
builder.Services.AddScoped<AtualizarClienteUseCase>();
builder.Services.AddScoped<ExcluirClienteUseCase>();

// CORS para permitir requisições do Blazor Web
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowBlazor");
app.MapControllers();

// Health check para UptimeRobot
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Cria/Migra o banco na inicialização com logging explícito
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