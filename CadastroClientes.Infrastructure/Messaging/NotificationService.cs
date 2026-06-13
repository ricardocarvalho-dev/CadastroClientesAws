using CadastroClientes.Application.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace CadastroClientes.Infrastructure.Messaging;

public class NotificationService : IMessagingService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IConnectionFactory _connectionFactory;
    private const string QUEUE_NAME = "fila_cadastro_clientes";

    public NotificationService(ILogger<NotificationService> logger, IConnectionFactory connectionFactory)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public Task PublicarCriacaoClienteAsync(Guid clienteId, string nome, string email, string celular)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            using var channel = connection.CreateModel(); // v6: CreateModel() em vez de CreateChannel()

            channel.QueueDeclare(                        // v6: síncrono, sem Async
                queue: QUEUE_NAME,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var mensagem = new
            {
                clienteId,
                nome,
                email,
                celular,
                dataCadastro = DateTime.UtcNow,
                tipo = "cliente.criado"
            };

            var json = JsonSerializer.Serialize(mensagem);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties(); // v6: CreateBasicProperties()
            properties.Persistent = true;
            properties.ContentType = "application/json";

            channel.BasicPublish(                        // v6: síncrono, sem Async
                exchange: string.Empty,
                routingKey: QUEUE_NAME,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Mensagem publicada na fila {Fila} para cliente {ClienteId}",
                QUEUE_NAME, clienteId);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Erro ao publicar mensagem no RabbitMQ para cliente {ClienteId}. Cliente salvo mas mensagem não enviada.", clienteId);
            return Task.CompletedTask; // ✅ Não derruba o cadastro se RabbitMQ falhar
            //throw;
        }
    }
}