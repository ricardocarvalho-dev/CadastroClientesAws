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

    public Task PublicarCriacaoClienteAsync(Guid clienteId, string nome, string email, string celular, string mensagem, string canal = "Email")
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: QUEUE_NAME,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var payload = new
            {
                clienteId,
                nome,
                email,
                celular,
                mensagem,
                canal,                          // ← novo campo
                dataCadastro = DateTime.UtcNow,
                tipo = "cliente.criado"
            };

            var json = JsonSerializer.Serialize(payload);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: QUEUE_NAME,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Mensagem publicada na fila {Fila} para cliente {ClienteId}. Canal: {Canal}",
                QUEUE_NAME, clienteId, canal);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar mensagem no RabbitMQ para cliente {ClienteId}. Cliente salvo mas mensagem não enviada.", clienteId);
            throw;
        }
    }
}