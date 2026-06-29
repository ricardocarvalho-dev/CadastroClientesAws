using Amazon.SQS;
using Amazon.SQS.Model;
using CadastroClientes.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CadastroClientes.Infrastructure.Messaging;

public class SqsNotificationService : IMessagingService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsNotificationService> _logger;
    private readonly string _queueUrl;

    public SqsNotificationService(
        IAmazonSQS sqsClient,
        ILogger<SqsNotificationService> logger,
        IConfiguration configuration)
    {
        _sqsClient = sqsClient;
        _logger = logger;
        _queueUrl = configuration["AWS:SqsQueueUrl"]
            ?? throw new InvalidOperationException("AWS:SqsQueueUrl não configurado.");
    }

    public async Task PublicarCriacaoClienteAsync(
        Guid clienteId, string nome, string email,
        string celular, string mensagem, string canal = "Email")
    {
        try
        {
            var payload = new
            {
                clienteId,
                nome,
                email,
                celular,
                mensagem,
                canal,
                dataCadastro = DateTime.UtcNow,
                tipo = "cliente.criado"
            };

            var json = JsonSerializer.Serialize(payload);

            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = json,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {
                        "ContentType", new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = "application/json"
                        }
                    }
                }
            };

            var response = await _sqsClient.SendMessageAsync(request);

            _logger.LogInformation(
                "Mensagem publicada no SQS para cliente {ClienteId}. MessageId: {MessageId}",
                clienteId, response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao publicar mensagem no SQS para cliente {ClienteId}.", clienteId);
            throw;
        }
    }
}